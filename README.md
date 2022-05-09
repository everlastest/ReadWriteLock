# <center>说明文档</center>

<center>19520107 王子轩</center>

[TOC]

<p style="page-break-before:always"></p>

## 1. 项目要求

- 支持同时多个线程进行并发的读访。 

- 可以支持多个线程进行写请求。

- 要求不能出现写饥饿现象。

- 可以使用 Monitor(Enter、Exit)、Event、Semphore 等基本同步原语，不要使用更高级的原语。

## 2. 原理分析

### 2.1 Semphore

#### 2.1.1 作用

此原语用来限制可同时访问某一资源或资源池的线程数。

- P操作：semaphore的WaitOne()方法
- V操作：semaphore的Release()方法

#### 2.1.2 使用

在读写锁中，至少需要两个Semphore信号量来实现互斥访问:

1. 信号量rw

   此信号量用于保证读者和写者的互斥访问，即保证读者的读操作和写者的写操作之间以及写操作与写操作之间不能同时进行。

   因此：

   - 因为写者之间时互斥关系，每一个写者在访问资源是都需要执行P(rw)操作请求资源，写者在写操作完成之后进行V(rw)操作释放资源
   - 为了阻止写操作在读操作时进行，在写完后的第一个读者访问资源时，也需要执行P(rw)操作请求资源，此次请求资源可供所有读者使用，因此之后的读者可直接进入，不需要执行P(rw)操作。当最后一个读者读完后进行V(rw)操作即可。

2. 信号量mutex

   此信号量用于维护读者数量变量readerNum的互斥更新

   - 在修改读者数量readerNum变量之前使用P(mutex)操作请求mutex，修改后使用V(mutex)操作释放mutex。

### 2.2 读写公平法

为了避免写饥饿的产生，需要增加一个信号量isWrite来控制读写公平。

- 读者在每次想要读文件时最先进行P(isWrite)操作，这样做的目的是让读者在没有写请求时才可以将读者加入读者队列。当增加readerNum后要在读操作开始之前就通过V(isWrite)来恢复写请求。
- 写者在每次要写时需要首先P(isWrite)用来控制之后的读写请求无法成功，直到此写操作完成后V(isWrite)，才允许其他的读者和写者公平竞争。

## 3. 代码实现

### 3.1 初始化变量

- readerNum： 记录读者数量（读者数量为0时才可以写）
- rw：保证读者和写者互斥访问（写和写以及写和读不能同时进行）
- mutex：用于读者互斥更新readerNum变量（两个读互斥更新readerNum）
- isWrite：用于实现读写公平竞争（在有写请求时拒绝之后其他读者的读请求，直到写完）

```c#
Class MyLock{
    
		private int readerNum;		// 记录读者数量（读者数量为0时才可以写）
        Semaphore rw = null;		// 保证读者和写者互斥访问（写和写以及写和读不能同时进行）
        Semaphore mutex = null;		// 用于读者互斥更新readerNum变量（两个读互斥更新readerNum）
        Semaphore isWrite = null;	// 用于实现读写公平竞争（在有写请求时拒绝之后其他读者的读请求，直到写完）
        // 初始化
    	public MyLock(int maxNum)
        {
            readerNum = 0;// 读者为0 
            rw = new Semaphore(1, maxNum);// 参数一为一开始的信号量个数，参数二为最大请求个数
            mutex = new Semaphore(1, maxNum);
            isWrite = new Semaphore(1, maxNum);
            Console.WriteLine("读写锁已被创建");
            Console.WriteLine("----------------------------------------");
        }
}
```

### 3.2 封装P操作和V操作

对原生的WaitOne和Release方法进行再次封装以简化代码。

```c#
 private void P(Semaphore S)
 {
 	S.WaitOne();
 }
 private void V(Semaphore S)
 {
 	S.Release();
 }
```

### 3.3 获取读锁

在读者读文件之前需要获取读锁

- 在无写请求时才能获取读锁
- 获取读锁时要增加readerNum数量，因此要获取互斥量mutex
- 如果这是第一个访问最新文件的读者需要获取读写信号量，防止写者在还有人读时进行写操作
- 更新readerNum后要马上释放mutex和isWrite信号量，恢复写请求和互斥更新readerNum的权利

```c#
 public void acquireReadLock()
 {
     P(isWrite);// 在无写请求时才能进入
     P(mutex);	// 互斥更新readerNum变量
     if (readerNum == 0)
     {
         P(rw);	// 第一个读进程时开启读写锁，只有读进程可进入
         Console.WriteLine("读者{0}获取读写锁", Thread.CurrentThread.ManagedThreadId);
     }
     Console.WriteLine("读者{0}开始读", Thread.CurrentThread.ManagedThreadId);
     readerNum++;
     V(mutex);	// 释放互斥更新readerNum的信号量
     V(isWrite);// 恢复对共享资源的写请求
 }
```

### 3.4 释放读锁

在读者读完文件时要释放读锁

- 释放读锁时要减少readerNum数量，因此要获取互斥量mutex
- 如果这是写操作之前的最后一个读者或者之后暂时没有新的读者，要释放读写信号量，允许写进程写入
- 在释放读锁的最后一步要将mutex信号量释放，允许其他读者更改readerNum

```c#
public void releaseReadLock()
{
    P(mutex);	// 互斥更新readerNum变量
    readerNum--;
    Console.WriteLine("读者{0}结束读", Thread.CurrentThread.ManagedThreadId);
    if (readerNum == 0)
    {
        V(rw);	// 释放读写锁，允许写进程写入
        Console.WriteLine("读者{0}释放读写锁", Thread.CurrentThread.ManagedThreadId);
    }
    V(mutex);	// 释放更新readerNum信号量    

}
```

### 3.5 获取写锁

在写者要写入文件时需要获取写锁

- 写者要写时需要告诉程序，以使之后的读者不再被允许读
- 写者只能再读操作和写操作都执行完成后才能进行

```c#
public void acquireWriteLock()
{
    P(isWrite);	// 告诉之后读者不再被允许读
    P(rw);		// 互斥访问共享文件
    Console.WriteLine("写者{0}获取读写锁并开始写", Thread.CurrentThread.ManagedThreadId);
}
```

### 3.6 释放写锁

再写者写完时要释放写锁

- 释放读写锁，允许写者或读者写入和读取
- 恢复共享文件状态为可写

```c#
 public void releaseWriteLock()
 {
     V(rw);      // 释放读写锁，允许写者或读者写入和读取
     V(isWrite); // 结束写操作，允许读者加入
     Console.WriteLine("写者{0}结束写并释放读写锁", Thread.CurrentThread.ManagedThreadId);
 }
```

## 4. 测试

### 4.1 测试程序

共10个线程，5个读线程，5个写线程，为保证读者和写者的随机性以及无序性，随机分配五个读线程来使读者和写者的顺序随机。

- 利用随机数从0-9中随机生成5个数字加入到哈希set中

  ```c#
   private static HashSet<int> getRandom()
   {
       Random random = new Random();
       HashSet<int> set = new HashSet<int>();
       int i = 0;
       while (i < 5){
           int r = random.Next(0, 10);
           if (!set.Contains(r)){
               set.Add(r);
               i++;
           }
       }
       return set;
   }
  ```

- 循环生成10个线程，每个线程根据哈希set中的值来指定身份为读者或写者

  ```c#
  for(int i = 0; i < 10; i++){
      if (readerIndex.Contains(i)){
          threads.Add(new Thread(() => { Read(); }));
          threads[i].Start();
      }
      else{
          threads.Add(new Thread(() => { Write(); }));
          threads[i].Start();
      }
  }
  ```

- 写者线程睡眠3秒

  ```c#
  private static void Write()
  {
      _lock.acquireWriteLock();
      Thread.Sleep(3000);//模拟写
      _lock.releaseWriteLock();
  }
  ```

- 读者线程睡眠1秒

  ```c#
  private static void Read()
  {
      _lock.acquireReadLock();
      Thread.Sleep(1000);//模拟读
      _lock.releaseReadLock();
  }
  ```

### 4.2 测试结果

​	<img src="D:\文件\课程\.net\测试\期中作业\image\image-20220506161234036.png" alt="image-20220506161234036" style="zoom: 67%;" />

### 4.3 结果分析

可以有5-14共10个个线程，其中线程5、6、7、9、13为读者，线程8、10、11、12、14为写者

- 可以看出读者5在获取读写锁后，之后的读操作6，7都不需要在获取读写锁，因此读操作是可以并发读取的
- 直到有了新的写操作8阻止了之后的9，13进行读，因此程序不会发生写饥饿的现象
- 而在写的3秒过程中，并没有其他的读操作和写操作开始，也说明读和写以及写和写是互斥的
- 其实在写的3秒过程中，后面的读者和写者也都产生了，但并不是写者连续的写，所以读者和写者也是可以公平竞争的，满座读写公平性。