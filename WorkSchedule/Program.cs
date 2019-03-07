using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Timers;

namespace WorkSchedule
{
    class Program
    {
        private static readonly string Host = "http://localhost/";

        static void Main(string[] args)
        {
            Timer timer = new Timer
            {
                Enabled = true,
                Interval = 60000
            };
            timer.Start();
            timer.Elapsed += new ElapsedEventHandler(CheckTimer);

            Console.ReadKey();
        }

        private static void CheckTimer(object source, ElapsedEventArgs e)
        {
            DateTime now = DateTime.UtcNow;
            int hour = now.Hour;
            int min = now.Minute;

            Console.WriteLine("Event worked at: " + now.ToString());

            if (min.Equals(0) || min.Equals(30))
            {
                RequestJob("DirectLine/CheckBoxStatus", null);
            }

            if((hour.Equals(3) || hour.Equals(8)) && min.Equals(30))
            {
                RequestJob("DirectLine/SendWaitingOrder", new Dictionary<string, object>() { { "DL", "IDS" } });
            }
        }

        private static void RequestJob(string url, Dictionary<string, object> parameters)
        {
            string queryString = "";
            if (parameters != null && parameters.Any())
            {
                queryString = "?" + string.Join("&", parameters.Select(p => p.Key + "=" + p.Value.ToString()).ToArray());
            }

            WebRequest request = WebRequest.Create(Host + url + queryString);

            Console.WriteLine(request.RequestUri);

            try
            {
                using (WebResponse response = request.GetResponse())
                {
                    response.Close();
                    Console.WriteLine("Event worked successfully!: " + DateTime.UtcNow.ToString());
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: " + e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message);
            }
        }
    }
}
