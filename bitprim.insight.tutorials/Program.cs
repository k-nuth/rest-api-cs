using System;

namespace bitprim.tutorials
{
    class Program
    {
        static void Main(string[] args)
        {
            var memoService = new MemoService(new BitprimInsightAPI());
            Console.WriteLine("Scraping...");
            var posts = memoService.GetLatestPosts(5);
            int i = 0;
            foreach(string post in posts)
            {
                Console.WriteLine((i+1) + ": " + post);
            }
            Console.WriteLine("Done! Press any key to finish");
            Console.ReadKey();
        }
    }
}
