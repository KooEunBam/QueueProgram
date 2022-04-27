using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
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
        enum Status { Start, Idle }; // thread1 상태표시 Start (시작), Idle(쉬고있는 상태)

        private readonly Queue<NewData> queue;
        private readonly Random random; 
        private readonly Mutex mutex; 

        private const int threadSleep = 10; // 쓰레드 반복주기
        private const int threadRunningTime = 10000; // 총 반복 시간

        private Thread th1;
        private Thread th2;
        Status status;

        public MainWindow()
        {
            this.queue = new Queue<NewData>();
            this.random = new Random();
            this.mutex = new Mutex();

            this.th1 = new Thread(new ThreadStart(Thread1));
            this.th2 = new Thread(new ThreadStart(Thread2));
            this.status = Status.Idle; // Thread 1, Idle상태

            InitializeComponent();
            th1.Start();
            th2.Start();
        }


        /// <summary>
        /// status가 Start인 경우 Start버튼이 Run으로 바뀌며 10초동안 (10000ms)
        /// 지정된 주기마다 반복하며 (10ms) seq값은 번호 순서, data값은 100미만의 난수를 저장한다.
        /// Enqueue에 Mutex를 걸어준 이유는 Enqueue를 실행도중 Thread2에서 Dequeue를 수행하게 되면
        /// 손실이 일어나므로 Mutex를 사용했다. 
        /// 모두 수행하고 난뒤 Run버튼은 원래 Start로 바뀌며 status는 Idle이 된다.
        /// </summary>

        private void Thread1()
        {
            while (true)
            {
                if (status == Status.Start) // status가 Start인 경우
                {
                    Dispatcher.Invoke(() =>
                        StartButton.Content = "Run"); // Start버튼을 Run으로 바꾸며

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
                        }
                        else // seq가 10으로 나누어 떨어지지 않는다면 seq값만 출력함
                        {
                            Dispatcher.Invoke(() =>
                                Queue_TextBox.Text = $"{Queue_TextBox.Text}{newData.seq} ");
                            mutex.WaitOne();
                            queue.Enqueue(newData);
                            mutex.ReleaseMutex();
                        }
                        Thread.Sleep(threadSleep); // threadSleep주기만큼 반복함
                    }
                    Dispatcher.Invoke(() => StartButton.Content = "Start"); // Run이였던 버튼을 Start로 바꿈
                    status = Status.Idle; // status는 Start에서 Idle로 바꿈
                }
                else
                {
                    Thread.Sleep(threadSleep); // 무한 루프돌면서 지속적인 메모리 사용을 막기위해 지정된 시간동안 스레드 일시 중단함
                }
            }
        }

        /// <summary>
        /// 큐에 포함된 요소가 있는지 판별하고 
        /// Dequeue 수행시 Mutex를 부여해 Thread1에서 코드블록이 동시에 실행되는것을 방지하며
        /// seq가 10번째마다 data를 출력하며 아닌경우에는 seq만 출력함
        /// </summary>

        private void Thread2()
        {
            while (true)
            {
                if (queue.Count != 0) // 큐에 포함된 요소가 있다면
                {
                    mutex.WaitOne(); // 뮤텍스를 취득 할 때까지 대기함
                    NewData newData = queue.Dequeue(); // 뮤텍스 취득후 Dequeue를 수행, newData에 대입함
                    mutex.ReleaseMutex(); // 뮤텍스 해제함

                    if (newData.seq % 10 == 0) // newData의 seq가 십의 배수번째마다 data를 출력함
                    {
                        Dispatcher.Invoke(() =>
                            Dequeue_TextBox.Text = $"{Dequeue_TextBox.Text}SEQ = {newData.seq} data = {newData.data}\n");
                    }
                    else // 십의 배수번째가 아닌경우 seq만 출력함
                    {
                        Dispatcher.Invoke(() =>
                            Dequeue_TextBox.Text = $"{Dequeue_TextBox.Text}{newData.seq} ");
                    }
                }
                else // 큐에 포함된 요소가 없다면 지정된 시간동안 현재 스레드 일시 중단함
                {
                    Thread.Sleep(threadSleep); 
                }
            }
        }

        /// <summary>
        /// Start버튼 클릭시
        /// status는 Status.Idle 상태에서 Status.Start로 바뀌게 되고
        /// 양옆에 있는 TextBox는 초기화 하게 된다.
        /// </summary>

        private void StartButton_Click(object sender, RoutedEventArgs e) // Idle 상태에서 버튼클릭시 Start로 바뀌고 초기화 진행
        {
            if (status == Status.Idle) // Status가 Idle상태에서 버튼을 누르면
            {
                Dequeue_TextBox.Text = ""; // TextBox를 초기화 한다
                Queue_TextBox.Text = "";
                status = Status.Start; // Status는 Start로 바뀌며
            }
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
