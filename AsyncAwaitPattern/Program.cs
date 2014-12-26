using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace AsyncAwaitPattern
{
    /// <summary>
    /// Demo usage of the TAP - Task Parallel Library
    /// ***Examples taken from Pro Asynchronous Programming with .NET by Apress***
    /// </summary>
    public class Program
    { 
        public static void Main(string[] args)
        {
            //Reflection to get all instances the implement IRunnable in runtime
            var type = typeof(IRunnable);
            var implTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(p => type.IsAssignableFrom(p) && !p.IsInterface);

            foreach (var implType in implTypes)
            {
                var instance = Activator.CreateInstance(implType) as IRunnable;
                Console.WriteLine("Running : {0}", instance.GetType().Name);
                instance.Run().Wait();
            }

            Console.ReadLine();
        }
    }

    //Basic usage
    //-----------------------------------------------
    public class Demo1 : IRunnable
    {
        public async Task Run()
        {
            await Task.Delay(1);
        }
    }

    //Dangers of Closures - 1
    //------------------------------------------------
    public class Demo2 : IRunnable
    {
        public async Task Run()
        {
            for (int i = 0; i < 10; i++)
            {
                ///prints all 10!
                Task.Factory.StartNew(() => Console.WriteLine(i));
            }

            await Task.Delay(1);

            Console.WriteLine("Press enter to continue...");
            Console.ReadLine();
        }
    }

/*
    because you are simply issuing work to the thread pool infrastructure, and thus you have no control over the order in
    which the tasks will run. Run the code and you will perhaps find a much more unexpected result. You will most likely
    have seen ten 10s on the screen. This seems very strange, as you may well have expected only to see a set of unique
    values from 0 to 9; but instead you see the same value printed out, and not even one of the expected values. The cause
    lies with the closure; the compiler will have had to capture local variable i and place it into a compiler-generated
    object on the heap, so that it can be referenced inside each of the lambdas. The question is, when does it create this
    object? As the local variable i is declared outside the loop body, the capture point is, therefore, also outside the loop
    body. This results in a single object being created to hold the value of i, and this single object is used to store each
    increment of i. Because each task will share the same closure object, by the time the first task runs the main thread
    will have completed the loop, and hence i is now 10. Therefore, all 10 tasks that have been created will print out the
    same value of i, namely 10.
 */

    //THE FIX
/* 
    To fix this problem, you need to change the point of capture. The compiler only captures the variables used
    inside the closure, and it delays the capture based on the scope of the variable declaration. Therefore, instead of
    capturing I, which is defined once for the lifetime of the loop, you need to introduce a new local variable, defined
    inside the loop body, and use this variable inside the closure. As a result, the variable is captured separately for
    iteration of the loop.
    Running the code in Listing 3-10 will now produce all values from 0 to 9; as for the order, well, that’s unknown
    and part of the fun of asynchronous programming. As stated earlier, closures are the most natural way to flow
    information into a task, but just keep in mind the mechanics of how they work, as this is a common area for bugs and
    they are not always as obvious to spot as this example.
 */

    //Dangers of Closures - 2
    //------------------------------------------------
    public class Demo3 : IRunnable
    {
        public async Task Run()
        {
            for (int i = 0; i < 10; i++)
            {
                int toCaptureI = i;
                Task.Factory.StartNew(() => Console.WriteLine(toCaptureI));
            }

            await Task.Delay(1);

            Console.WriteLine("Press enter to continue...");
            Console.ReadLine();
        }
    }

    //Returning Data from a Task
    //---------------------------------------------------
/*  Up to now we have just examined the notion of passing data into a task, but tasks can also be used to return results.
    Have you ever wondered what the chances are of winning a lottery that has 49,000 possible numbers of which you
    have to pick the 600 that are selected on the given night? You probably already know your chances of winning are slim,
    but Listing 3-11 contains partial implementation of the code necessary to calculate the odds.*/

    public class Demo4 : IRunnable
    {
        public async Task Run()
        {
            BigInteger n = 49000;
            BigInteger r = 600;
            BigInteger part1 = MathUtils.Factorial(n);
            BigInteger part2 = MathUtils.Factorial(n - r);
            BigInteger part3 = MathUtils.Factorial(r);

            BigInteger chances = part1 / (part2 * part3);

            Console.WriteLine("chances are : {0}", chances);

            await Task.Delay(1);
        }
    }

    /*
    Executing this code sequentially will only use one core; however, since the calculation of part1, part2, and part3
    are all independent of one another, you could potentially speed things up if you calculated those different parts as
    separate tasks. When all the results are in, do the simple divide-and-multiply operation—TPL is well suited for this
    kind of problem. Listing 3-12 shows the refactored code that takes advantage of TPL to potentially calculate all the
    parts at the same time.
 */

    public class Demo5 : IRunnable
    {
        public async Task Run()
        {
            BigInteger n = 49000;
            BigInteger r = 600;

            //run in parallel
            Task<BigInteger> part1 = Task.Factory.StartNew<BigInteger>(() => MathUtils.Factorial(n));
            Task<BigInteger> part2 = Task.Factory.StartNew<BigInteger>(() => MathUtils.Factorial(n - r));
            Task<BigInteger> part3 = Task.Factory.StartNew<BigInteger>(() => MathUtils.Factorial(r));

            BigInteger chances = part1.Result / (part2.Result * part3.Result);

            Console.WriteLine("chances are : {0}", chances);

            await Task.Delay(1);
        }
    }

    public class MathUtils
    {
        public static BigInteger Factorial(BigInteger n)
        {
            BigInteger result = n;
            for (int i = 1; i < n; i++)
            {
                result = result * i;
            }

            return result;
        }
    }

/*  public Task<TResult> StartNew<TResult>(Func<TResult> function);
    The generic argument TResult identifies the type of result the task will return. For the task to be able to return a
    result of this type, the signature of the delegate used to represent the task body will also need an identical return type.
    So instead of supplying an Action delegate, now you use a Func<TResult>.Furthermore, StartNew<T> now returns
    not a Task but a Task<TResult>.This has an additional property called Result, which is used to obtain the result of
    the asynchronous operation.This property can only yield the result once the asynchronous operation has completed,
    so making a call to this property will block the calling thread until the asynchronous operation has completed. In
    Listing 3-12 three tasks will be created, each performing its part of the calculation in parallel.Meanwhile, the main
    thread moves forward to attempt to calculate the overall chance by combining the results from all the parts. As each
    part result is required by the main thread, it blocks until that result is available before evaluating the next part of the
    expression.
    One key advantage TPL has over previous asynchronous APIs is that the asynchronous code in Listing 3-12 is not
    radically different in structure from the sequential code in Listing 3-11. This is in general contrast to asynchronous
    APIs of the past, which often required radical change to the structure of the algorithm, thus often overcomplicating
    the code.This was one of the key API guidelines.*/

    public class Demo6 : IRunnable
    {
        public async Task Run()
        {
            Console.WriteLine("downloading page in synchronous (blocking)");

            string download = DownloadWebPage("http://www.rocksolidknowledge.com/5SecondPage.aspx");
            Console.WriteLine(download);

            await Task.Delay(1);

            Console.WriteLine("downloading page in asynchronous way (non blocking)");

            Task<string> downloadTask = DownloadWebPageAsync("http://www.rocksolidknowledge.com/5SecondPage.aspx");
            while (!downloadTask.IsCompleted)
            {
                Console.Write(".");
                Thread.Sleep(250);
            }

            Console.WriteLine(downloadTask.Result);

            Console.WriteLine("downloading page in asynchronous way (non blocking) - Better Version");

            Task<string> downloadTask1 = BetterDownloadWebPageAsync("http://www.rocksolidknowledge.com/5SecondPage.aspx");
            while (!downloadTask1.IsCompleted)
            {
                Console.Write(".");
                Thread.Sleep(250);
            }

            Console.WriteLine(downloadTask1.Result);
        }

        //synchronous 
        private string DownloadWebPage(string url)
        {
            WebRequest request = WebRequest.Create(url);
            WebResponse response = request.GetResponse();
            var reader = new StreamReader(response.GetResponseStream());
            {
                // this will return the content of the web page
                return reader.ReadToEnd();
            }
        }

        //async
        private Task<string> DownloadWebPageAsync(string url)
        {
            return Task.Factory.StartNew(() => DownloadWebPage(url));
        }

        /*  
       The IsCompleted property on the task allows you to determine if the asynchronous operation has completed.
       While it is still completing, the task keeps the user happy by displaying dots; once it completes, you request the result
       of the task.Since you know the task has now completed, the result will immediately be displayed to the user.
       This all looks good until you start to analyze the cost of achieving this asynchronous operation. In effect you now
       have two threads running for the duration of the download: the one running inside Main and the one attempting to get
       the response from the web site.The thread responsible for getting the content is spending most of its time blocked on
       the line reader.ReadToEnd(); you have taken a thread from the thread pool, denying others the chance to use it, only
       for it to spend most of the time idle.

       A far more efficient approach would be to create a thread to request the data from the web server, give the thread
       back to the thread pool, and when the data arrives, obtain a thread from the pool to process the results.

       Although this approach feels far more complex than simply wrapping a piece of long-running code in a task, it
       is a far more efficient use of the thread pool, keeping the number of threads as small as possible for the same level of
       concurrency. This is advantageous because threads are not free to create and consume memory.
  */

        private Task<string> BetterDownloadWebPageAsync(string url)
        {
            WebRequest request = WebRequest.Create(url);
            IAsyncResult ar = request.BeginGetResponse(null, null);
            Task<string> downloadTask =
            Task.Factory
            .FromAsync<string>(ar, iar =>
            {
                using (WebResponse response = request.EndGetResponse(iar))
                {
                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        return reader.ReadToEnd();
                    }
                }
            });
            return downloadTask;
        }
    }

    //Error Handling
    public class Demo7 : IRunnable
    {
        public async Task Run()
        {
            Task task = Task.Factory.StartNew(() => Import(@"C:\data\2.xml"));
            try
            {
                task.Wait();
            }
            catch (AggregateException errors)
            {
                foreach (Exception error in errors.Flatten().InnerExceptions)
                {
                    Console.WriteLine("{0} : {1}", error.GetType().Name, error.Message);
                }
            }

            await Task.Delay(1);
        }

        private void Import(string cDataXml)
        {
            XElement doc = XElement.Load(cDataXml);
            // process xml document
        }
    }


    /*  In Listing 3-17, even if the Import method fails before we hit the Wait method call, the task object will hold onto
    the exception and re-throw it when the call to Wait is made.This all looks nice and simple—however, there is a twist.
    Let us say that the 2.xml file contains invalid XML.It would therefore seem logical that the type of exception being
    delivered would be an XML-based one. In fact what you will get is an AggregateException. This seems a little odd
    at first, since AggregateException implies many exceptions, and in this situation it looks like you could only get one
    exception. As you will see later in this chapter, tasks can be arranged in a parent/child relationship, whereby a parent
    won’t be deemed completed until all its children have been. If one or many of those children complete in a faulted
    state, that information needs to propagated, and for that reason TPL will always wrap task-based exceptions with an
    AggregateException.
    The role of an exception is twofold: first, to provide some error message that is normally understood by a
    developer; and second, to indicate the type of fault so that we construct the appropriate catch blocks to handle or
    recover from the error.Catching an AggregateException is probably not detailed enough to find the root cause of
    the error, so you need to crack open the aggregate exception and examine the underlying exceptions. The underlying
    exceptions are found via the InnerExceptions property on the AggregateException*/
    /*   This is somewhat cumbersome, made even more so in that it is possible that a given inner exception can also be
        an aggregate exception requiring another level of iteration.Thankfully the AggregateException type has a Flatten
       method that will provide a new AggregateException that contains a single, flat set of inner exceptions(Listing 3-19).*/

    public class Demo8 : IRunnable
    {
        public async Task Run()
        {
            Task task = Task.Factory.StartNew(() => Import(@"C:\data\2.xml"));
            try
            {
                task.Wait();
            }

            catch (AggregateException errors)
            {
                errors.Handle(IgnoreXmlErrors);
            }

            await Task.Delay(1);
        }

        private void Import(string cDataXml)
        {
            XElement doc = XElement.Load(cDataXml);
            // process xml document
        }

        private static bool IgnoreXmlErrors(Exception arg)
        {
            return (arg.GetType() == typeof (XmlException));
        }
    }

    /*As explained earlier, the role of an exception handler is to look at the exception type and decide how to recover
    from the error.In the case of AggregateException errors, that would mean iterating through all the inner exceptions,
    examining each type in turn, deciding if it can be handled, and if not re-throw it and possibly any others that can’t be
    handled.
    This would be extremely tedious.So there is a method on the AggregateException called Handle, which
    reduces the amount of code you would need to write, and perhaps gets closer to the traditional try/catch block.The
    Handle method takes a predicate style delegate that is given each exception in turn from the AggregateException.
    The delegate should return true if the exception has been handled and false if the exception can't be handled.
    At the end of processing all the exceptions, any exceptions not handled will be re-thrown again as part of a new
    AggregateException containing just those exceptions deemed to have been unhandled.
    Listing 3-20 shows code that catches an AggregateException and then only ignores the exception if it contains
    XML-based exceptions. Any other type of exception contained inside the AggregateException will be re-thrown as
    part of a new AggregateException.In essence the developer considers XML-based errors as not fatal, and is happy for
    the application to continue.*/
}
