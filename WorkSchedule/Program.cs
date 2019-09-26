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
        private static readonly string PO_Host = "http://localhost:8080/";

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

            if (min.Equals(0))
            {
                RequestJob(PO_Host, "Ajax/CheckTracking", null);
            }

            if (min.Equals(0) || min.Equals(30))
            {
                RequestJob(Host, "DirectLine/CheckBoxStatus", null);
            }

            if (hour.Equals(6) && (min.Equals(45) || min.Equals(50) || min.Equals(55)))
            {
                RequestJob(Host, "DirectLine/TrackDirectLine", new Dictionary<string, object>() { { "DL", "IDS (US)" } });
            }

            if((hour.Equals(3) || hour.Equals(8)) && min.Equals(55))
            {
                RequestJob(Host, "DirectLine/SendWaitingOrder", new Dictionary<string, object>() { { "DL", "IDS US" } });
            }

            if (hour.Equals(15) && min.Equals(0))
            {
                RequestJob(PO_Host, "Test/DoSkuSync", null);
            }
        }

        private static void RequestJob(string host, string url, Dictionary<string, object> parameters)
        {
            string queryString = "";
            if (parameters != null && parameters.Any())
            {
                queryString = "?" + string.Join("&", parameters.Select(p => p.Key + "=" + p.Value.ToString()).ToArray());
            }

            WebRequest request = WebRequest.Create(host + url + queryString);

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
