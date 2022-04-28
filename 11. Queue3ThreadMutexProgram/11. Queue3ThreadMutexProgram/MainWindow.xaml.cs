using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using UsingQueueMutexProgram.Common;

namespace UsingQueueMutexProgram
{
    /// <summary>
    /// Status는 Thread 1의 상태를 표시한다. 
    /// Start (Start버튼 누르면 상태가 Start로 바뀜), 
    /// Idle(Thread1이 모두 다 수행하고 쉬고있는 상태일때를 나타냄)
    /// 
    /// threadSleep은 쓰레드 반복주기이며, threadRunningTime은 총 반복 시간이다.
    /// Queue에 readonly를 사용한 이유는 Main함수에서만 초기화를 하며, 
    /// 다른 함수에서 초기화를 해야할 필요성이 보이지 않았기 때문입니다.
    /// </summary>

    public partial class MainWindow : Window
    {
        enum Status { Start, Running, Idle }; // thread1 상태표시 Start (시작), Running(작동상태), Idle(쉬고있는 상태)

        private readonly Queue<NewData> queue;
        private readonly Random random;
        private readonly Mutex mutex;
        private readonly Mutex mutex1;

        private const int threadSleep = 10; // 쓰레드 반복주기
        private const int threadRunningTime = 10000; // 총 반복 시간

        private int queue_textcount; // Enqueue 출력 할때마다 1개씩 올라감
        private int dequeue_textcount; // Dequeue 출력 할때마다 1개씩 올라감
        private int textclearcount; // 일정갯수되면 초기화 시키기 위해 필요한 변수
        private Thread th1;
        private Thread th2;
        private Thread th3;

        Status status;


        public MainWindow()
        {
            this.queue = new Queue<NewData>();
            this.random = new Random();
            this.mutex = new Mutex();
            this.mutex1 = new Mutex();

            this.th1 = new Thread(new ThreadStart(Thread1));
            this.th2 = new Thread(new ThreadStart(Thread2));
            this.th3 = new Thread(new ThreadStart(Thread3));
            this.status = Status.Idle; // Thread 1, Idle상태

            this.queue_textcount = 0;
            this.dequeue_textcount = 0;
            this.textclearcount = 70; // 70개 출력하면 초기화

            InitializeComponent();
            th1.Start();
            th2.Start();
            th3.Start();
        }


        /// <summary>
        /// status가 Start인 경우 Start버튼이 Run으로 바뀌며 10초동안 (10000ms)
        /// 지정된 주기마다 반복하며 (10ms) seq값은 번호 순서, data값은 100미만의 난수를 저장한다.
        /// Enqueue에 Mutex를 걸어준 이유는 Enqueue를 실행도중 Thread2에서 Dequeue를 수행하게 되면
        /// 손실이 일어나므로 Mutex를 사용했다. 
        /// 추가적으로 Dequeue()가 물리게되면 서로 같은 값을 출력할 수 있어서 모든 쓰레드에 같은 뮤텍스를 지정하였다.
        /// 모두 수행하고 난뒤 Run버튼은 원래 Start로 바뀌며 status는 Idle이 된다.
        /// </summary>

        private void Thread1()
        {
            while (true)
            {
                if (status == Status.Start) // status가 Start혹은 Running인 경우
                {
                    Dispatcher.Invoke(() =>
                        StartButton.Content = "Run"); // Start버튼을 Run으로 바꾸며
                    status = Status.Running; 

                    for (int i = 1; i <= threadRunningTime / threadSleep; i++) // 10초동안 threadsleep주기로 반복
                    {
                        NewData newData = new NewData(i, random.Next(100)); // seq값(번호순서), data에는 랜덤 숫자(100미만 정수)를 가진 newData 생성자

                        if (newData.seq % 10 == 0) // seq가 10으로 나누어 떨어질마다 seq와 data값을 출력하며, 
                        {
                            Dispatcher.Invoke(() =>
                                Queue_TextBox.Text = $"{Queue_TextBox.Text}SEQ = {newData.seq} data = {newData.data}\n");

                            mutex.WaitOne();
                            queue.Enqueue(newData);
                            mutex.ReleaseMutex();
                            queue_textcount++; // textcount값 1 증가.
                        }
                        else // seq가 10으로 나누어 떨어지지 않는다면 seq값만 출력함
                        {
                            Dispatcher.Invoke(() =>
                                Queue_TextBox.Text = $"{Queue_TextBox.Text}{newData.seq} ");
                            mutex.WaitOne();
                            queue.Enqueue(newData);
                            mutex.ReleaseMutex();
                            queue_textcount++; // textcount값 1 증가.
                        }
                        if (queue_textcount == textclearcount) // 특정갯수(textclearcount)가 되면 textcount는 0으로 되며, 초기화를 진행함 
                        {
                            queue_textcount = 0;

                            Dispatcher.Invoke(() =>
                            {
                                Queue_TextBox.Text = "";
                            }); // TextBox 초기화
                        }

                        if (status == Status.Idle)
                        {
                            break; // Running 상태에서 버튼을 누르면 for문 탈출 후 idle상태로 변화.
                        }
                        Thread.Sleep(threadSleep); // threadSleep주기만큼 반복함
                    }
                    Dispatcher.Invoke(() => StartButton.Content = "Start"); // Run이였던 버튼을 Start로 바꿈
                    status = Status.Idle; // status는 Start에서 Idle로 바꿈
                    queue_textcount = 0; // textcount값 초기화 해야함.
                }
                else
                {
                    Thread.Sleep(threadSleep); // 무한 루프돌면서 지속적인 메모리 사용을 막기위해 지정된 시간동안 스레드 일시 중단함
                }
            }
        }

        /// <summary>
        /// thread 2 -> 소비자
        /// queue.Count 와 Dequeue() 수행시 Thread1,Thread3와 겹치지 않게 lock함수를 이용함.
        /// newData는 Thread3에서 Dequeue() 수행시 손실이 일어나지 않게 각각 지역변수로 선언함.
        /// </summary>

        private void Thread2()
        {
            while (true)
            {
                NewData newData = new NewData(); // 지역변수 선언

                mutex.WaitOne();
                if (queue.Count != 0) // queue에 포함된 요소가 있다면
                {
                    newData = queue.Dequeue(); // newData에 Dequeue() 대입
                }
                else
                {
                    newData = null; // 포함된 요소가 없다면 예외발생하므로, null 대입
                }
                mutex.ReleaseMutex();

                if (newData != null)
                {
                    if (newData.seq % 10 == 0) // newData의 seq가 십의 배수번째마다 data를 출력함
                    {
                        Dispatcher.Invoke(() =>
                            Dequeue_TextBox.Text = $"{Dequeue_TextBox.Text}SEQ = {newData.seq} data = {newData.data}\n");
                        dequeue_textcount++;
                    }
                    else // 십의 배수번째가 아닌경우 seq만 출력함
                    {
                        Dispatcher.Invoke(() =>
                            Dequeue_TextBox.Text = $"{Dequeue_TextBox.Text}{newData.seq} ");
                        dequeue_textcount++;
                    }
                }

                mutex1.WaitOne(); // Thread2,3 가 동시에 접근하면 안되는 변수라서 mutex지정했습니다. 
                if (dequeue_textcount == textclearcount) // 특정갯수(textclearcount)가 되면 textcount는 0으로 되며, 초기화를 진행함 
                {
                    dequeue_textcount = 0;
                    Dispatcher.Invoke(() =>
                    {
                        Dequeue_TextBox.Text = "";
                    }); // TextBox 초기화
                }
                mutex1.ReleaseMutex();

                if (status == Status.Idle)
                {
                    mutex1.WaitOne();
                    dequeue_textcount = 0;
                    mutex1.ReleaseMutex();
                }

                Thread.Sleep(threadSleep);
            }
        }


        /// <summary>
        /// thread 3 -> 소비자
        /// queue.Count 와 Dequeue() 수행시 Thread1,Thread2와 겹치지 않게 lock함수를 이용함.
        /// newData는 Thread3에서 Dequeue() 수행시 손실이 일어나지 않게 각각 지역변수로 선언함.
        /// </summary>

        private void Thread3()
        {
            while (true)
            {
                NewData newData = new NewData(); // 지역변수 선언 

                mutex.WaitOne();
                if (queue.Count != 0) // queue에 포함된 요소가 있다면
                {
                    newData = queue.Dequeue(); // newData에 Dequeue() 대입 
                }
                else
                {
                    newData = null; // 포함된 요소가 없다면 예외발생하므로, null 대입
                }
                mutex.ReleaseMutex();

                if (newData != null)
                {
                    if (newData.seq % 10 == 0) // newData의 seq가 십의 배수번째마다 data를 출력함
                    {
                        Dispatcher.Invoke(() =>
                            Dequeue_TextBox.Text = $"{Dequeue_TextBox.Text}SEQ = {newData.seq} data = {newData.data}\n");
                        dequeue_textcount++;
                    }
                    else // 십의 배수번째가 아닌경우 seq만 출력함
                    {
                        Dispatcher.Invoke(() =>
                            Dequeue_TextBox.Text = $"{Dequeue_TextBox.Text}{newData.seq} ");
                        dequeue_textcount++;
                    }
                }

                mutex1.WaitOne(); // Thread2,3 가 동시에 접근하면 안되는 변수라서 mutex지정했습니다. 
                if (dequeue_textcount == textclearcount) // 특정갯수(textclearcount)가 되면 textcount는 0으로 되며, 초기화를 진행함 
                {
                    dequeue_textcount = 0;
                    Dispatcher.Invoke(() =>
                    {
                        Dequeue_TextBox.Text = "";
                    }); // TextBox 초기화
                }
                mutex1.ReleaseMutex();

                if (status == Status.Idle)
                {
                    mutex1.WaitOne();
                    dequeue_textcount = 0;
                    mutex1.ReleaseMutex();
                }

                Thread.Sleep(threadSleep);
            }
        }


        /// <summary>
        /// Start버튼 클릭시
        /// TextBox를 먼저 초기화 하며 
        /// (status.start를 먼저 하게되면, 짧은 순간에 Thread1이 실행될 수 있음)
        /// status는 Status.Idle 상태에서 Status.Start로 바뀌게 된다
        /// status가 Running상태에서 버튼을 누르면 Idle상태로 변환된다.
        /// </summary>

        private void StartButton_Click(object sender, RoutedEventArgs e) // Idle 상태에서 버튼클릭시 Start로 바뀌고 초기화 진행
        {
            if (status == Status.Idle) // Status가 Idle상태에서 버튼을 누르면
            {
                Dequeue_TextBox.Text = ""; // TextBox를 초기화 한다
                Queue_TextBox.Text = "";
                status = Status.Start; // Status는 Start로 바뀐다. 
            }
            else if (status == Status.Running) // Status가 Running상태가 되면
            {
                status = Status.Idle; // Status가 Idle상태로 변함
            }
        }

        /// <summary> 
        /// 텍스트 상자의 내용이 변경되면, 끝까지 Scroll하는 이벤트
        /// </summary>

        private void Queue_TextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            Queue_TextBox.ScrollToEnd();
        }

        private void Dequeue_TextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            Dequeue_TextBox.ScrollToEnd();
        }


        /// <summary> 
        /// 메인창 종료시 모든 쓰레드를 멈추며 어플리케이션 종료하는 이벤트
        /// </summary>

        private void MainWindow_Closed(object sender, EventArgs e) // Close()이벤트 발생시
        {
            Application.Current.Shutdown(); // 어플리케이션을 종료
            Environment.Exit(0); // 어플리케이션의 모든 쓰레드를 멈추어 종료시킴
        }
    }
}
