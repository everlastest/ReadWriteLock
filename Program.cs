using System;
using System.Collections.Generic;
using System.Threading;

/*
 * 1952107 王子轩
 * 利用基础信号量Semaphore完成读写公平的读写锁
 */
namespace ReadWriteLock
{
    class MyLock
    {
        private int readerNum;//记录读者数量
        Semaphore rw = null;//保证读者和写者互斥访问
        Semaphore mutex = null;//用于读者互斥更新readerNum变量
        Semaphore isWrite = null;//用于实现写优先
        public MyLock(int maxNum)
        {
            readerNum = 0;
            rw = new Semaphore(1, maxNum);
            mutex = new Semaphore(1, maxNum);
            isWrite = new Semaphore(1, maxNum);
            Console.WriteLine("读写锁已被创建");
            Console.WriteLine("----------------------------------------");
        }
        private void P(Semaphore S)
        {
            S.WaitOne();
        }
        private void V(Semaphore S)
        {
            S.Release();
        }
        public void acquireReadLock()
        {
            P(isWrite);//在无写请求时进入
            P(mutex);//互斥更新readerNum变量
            if (readerNum == 0)
            {
                P(rw);//第一个读进程时开启读写锁，只有读进程可进入
                Console.WriteLine("读者{0}获取读写锁", Thread.CurrentThread.ManagedThreadId);
            }
            Console.WriteLine("读者{0}开始读", Thread.CurrentThread.ManagedThreadId);
            readerNum++;
            V(mutex);//释放更新readerNum信号量
            V(isWrite);//恢复对共享资源的写请求
        }
        public void releaseReadLock()
        {
            P(mutex);//互斥更新readerNum变量
            readerNum--;
            Console.WriteLine("读者{0}结束读", Thread.CurrentThread.ManagedThreadId);
            if (readerNum == 0)
            {
                V(rw);//释放读写锁，允许写进程写入
                Console.WriteLine("读者{0}释放读写锁", Thread.CurrentThread.ManagedThreadId);
            }
            V(mutex);//释放更新readerNum信号量    
           
        }
        public void acquireWriteLock()
        {
            P(isWrite); // 告诉之后读者不再被允许读
            P(rw);      // 互斥访问共享文件
            Console.WriteLine("写者{0}获取读写锁并开始写", Thread.CurrentThread.ManagedThreadId);
        }
        public void releaseWriteLock()
        {
            V(rw);      // 释放读写锁，允许写者或读者写入和读取
            V(isWrite); // 结束写操作，允许读者加入
            Console.WriteLine("写者{0}结束写并释放读写锁", Thread.CurrentThread.ManagedThreadId);
        }
    }
    public class Test
    {
        private static readonly MyLock _lock = new MyLock(5);

        //共10个线程，随机指定其中5个为写线程
        private static HashSet<int> getRandom()
        {
            Random random = new Random();
            HashSet<int> set = new HashSet<int>();
            int i = 0;
            while (i < 5)
            {
                int r = random.Next(0, 10);
                if (!set.Contains(r))
                {
                    set.Add(r);
                    i++;
                }
            }
            return set;
        }
        private static void Read()
        {
            _lock.acquireReadLock();
            Thread.Sleep(1000);//模拟读
            _lock.releaseReadLock();
        }

        private static void Write()
        {
            _lock.acquireWriteLock();
            Thread.Sleep(3000);//模拟写
            _lock.releaseWriteLock();
        }
        public static void Main(string[] args)
        {
            List<Thread> threads = new List<Thread>();
            HashSet<int> readerIndex = getRandom();
            for(int i = 0; i < 10; i++)
            {
                if (readerIndex.Contains(i))
                {
                    threads.Add(new Thread(() => { Read(); }));
                    threads[i].Start();
                }
                else
                {
                    threads.Add(new Thread(() => { Write(); }));
                    threads[i].Start();
                }
            }
            threads.ForEach(thread => thread.Join());
        }
    }
}
