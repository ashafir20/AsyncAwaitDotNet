using System;
using System.Linq;
using System.Numerics;
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
}
