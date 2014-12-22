using System;
using System.Linq;
using System.Threading.Tasks;

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
            Console.WriteLine("Running Demo1...");
            await Task.Delay(1);
        }
    }

    //Dangers of Closures - 1
    //------------------------------------------------
    public class Demo2 : IRunnable
    {
        public async Task Run()
        {
            Console.WriteLine("Running Demo2...");

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
            Console.WriteLine("Running Demo3...");

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

}
