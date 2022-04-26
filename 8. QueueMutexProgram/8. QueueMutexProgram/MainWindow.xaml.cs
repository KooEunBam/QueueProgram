using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using UsingQueueMutexProgram.Common;

namespace UsingQueueMutexProgram
{
    /// <summary>
    /// 
    /// 
    /// 
    /// </summary>

    public partial class MainWindow : Window
    {
        enum Status { Start, Idle }; // thread1 상태표시 Start (시작), Idle(쉬고있는 상태)

        private readonly Queue<NewData> queue; 
        private readonly Random random;
        private readonly Mutex mutex;

        private const int threadSleep = 10;

        private Thread th1;
        private Thread th2;
        Status status;


        /// <summary>
        /// 
        /// 
        /// 
        /// </summary>

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
        /// 
        /// 
        /// 
        /// </summary>

        private void Thread1()
        {
            while (true)
            {
                if (status == Status.Start)
                {
                    Dispatcher.Invoke(() =>
                        StartButton.Content = "Run");

                    for (int i = 1; i <= 100; i++)
                    {
                        NewData newData = new NewData(i, random.Next(100));

                        if (newData.seq % 10 == 0)
                        {
                            Dispatcher.Invoke(() =>
                                Queue_TextBox.Text = $"{Queue_TextBox.Text}SEQ = {newData.seq} data = {newData.data}\n");

                            mutex.WaitOne();
                            queue.Enqueue(newData);
                            mutex.ReleaseMutex();
                        }
                        else
                        {
                            Dispatcher.Invoke(() =>
                                Queue_TextBox.Text = $"{Queue_TextBox.Text}{newData.seq} ");
                            mutex.WaitOne();
                            queue.Enqueue(newData);
                            mutex.ReleaseMutex();
                        }
                        Thread.Sleep(threadSleep);
                    }
                    Dispatcher.Invoke(() => StartButton.Content = "Start");
                    status = Status.Idle;
                }
                else
                {
                    Thread.Sleep(threadSleep);
                }
            }
        }

        /// <summary>
        /// 
        /// 
        /// 
        /// </summary>

        private void Thread2()
        {
            while (true)
            {
                if (queue.Count != 0)
                {
                    mutex.WaitOne();
                    NewData newData = queue.Dequeue();
                    mutex.ReleaseMutex();

                    if (newData.seq % 10 == 0)
                    {
                        Dispatcher.Invoke(() =>
                            Dequeue_TextBox.Text = $"{Dequeue_TextBox.Text}SEQ = {newData.seq} data = {newData.data}\n");
                    }
                    else
                    {
                        Dispatcher.Invoke(() =>
                            Dequeue_TextBox.Text = $"{Dequeue_TextBox.Text}{newData.seq} ");
                    }
                }
                else
                {
                    Thread.Sleep(threadSleep);
                }
            }
        }

        /// <summary>
        /// 
        /// 
        /// 
        /// </summary>

        private void StartButton_Click(object sender, RoutedEventArgs e) // Idle 상태에서 버튼클릭시 Start로 바뀌고 초기화 진행
        {
            if (status == Status.Idle)
            {
                status = Status.Start;
                Dequeue_TextBox.Text = "";
                Queue_TextBox.Text = "";
            }
        }

        /// <summary>
        /// 
        /// 
        /// 
        /// </summary>

        private void MainWindow_Closed(object sender, EventArgs e) // 메인창 종료시 어플리케이션 강제종료 수행
        {
            Application.Current.Shutdown();
            Environment.Exit(0);
        }
    }
}
