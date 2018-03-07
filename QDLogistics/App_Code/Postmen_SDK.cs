using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace Postmen_sdk_NET
{
    public class Postmen
    {
        private const string version = "v3";
        private string api_key;
        private string endpoint;
        private bool retry;
        private bool rate;

        private Dictionary<string, string> keyList = new Dictionary<string, string>()
        {
            { "production", "bc70a9dc-7f52-4815-8a7d-28603be17f6d" },
            { "sandbox", "e523abb1-507a-4d76-8877-8e052f2f411a" }
        };

        public Postmen(string region = "", string endpoint = "", bool retry = true, bool rate = true)
        {
            if (string.IsNullOrEmpty(keyList[region]))
            {
                throw new Exception("missed API key");
            }

            api_key = keyList[region];

            if (string.IsNullOrEmpty(region) && String.IsNullOrEmpty(endpoint))
            {
                throw new Exception("missed region");
            }

            if (string.IsNullOrEmpty(endpoint))
            {
                endpoint = "https://" + region + "-api.postmen.com";
            }

            this.endpoint = endpoint;
            this.retry = retry;
            this.rate = rate;
        }

        public JObject call(string method, string path, JObject body = null, string query = "", bool retry = true)
        {
            string endpoint = this.endpoint + path;
            int tries = 0;
            int maxtries = 4;
            int aux_error;
            JObject result = null;
            Exception exception = null;

            if (!string.IsNullOrEmpty(query)) endpoint += "?" + query;

            do
            {
                try
                {
                    string result_json = request(endpoint, method, body);
                    result = JObject.Parse(result_json);
                    aux_error = (int)result["meta"]["code"];

                    while (aux_error > 10) aux_error /= 10;

                    if (aux_error >= 2 && aux_error < 4)
                    {
                        return result;
                    }

                    else if ((bool)result["meta"]["retryable"] == false)
                    {
                        retry = false;
                        throw PostmenException.FactoryMethod(result);
                    }
                    else
                    {
                        ++tries;
                    }
                }
                catch (Exception e)
                {
                    exception = e;
                    ++tries;

                }
            } while (retry && tries < maxtries);

            if (exception != null) throw exception;

            throw PostmenException.FactoryMethod(result);
        }

        public string request(string endpoint, string method, JObject body)
        {

            string result_json = "";

            HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(endpoint);
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = method;
            httpWebRequest.Headers["postmen-api-key"] = api_key;
            httpWebRequest.Headers["x-postmen-agent"] = "NET-sdk-" + version;

            if (!(body == null))
            {
                using (StreamWriter streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                {
                    streamWriter.Write(body.ToString());
                    streamWriter.Flush();
                    streamWriter.Close();
                }
            }

            HttpWebResponse httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            using (StreamReader streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                result_json = streamReader.ReadToEnd();
            }

            return result_json;
        }

        public JObject GET(string path, string query = "", bool retry = true)
        {
            return call(method: "GET", path: path, query: query, retry: retry);
        }

        public JObject POST(string path, JObject body = null, string query = "", bool retry = true)
        {
            return call(method: "POST", path: path, body: body, query: query, retry: retry);
        }

        public JObject PUT(string path, JObject body = null, string query = "", bool retry = true)
        {
            return call(method: "PUT", path: path, body: body, query: query, retry: retry);
        }

        public JObject DELETE(string path, string query = "", bool retry = true)
        {
            return call(method: "DELETE", path: path, query: query, retry: retry);
        }

        public JObject get(string resource, string id = "", string query = "", bool retry = true)
        {
            return GET(path: "/" + version + "/" + resource + "/" + id, query: query, retry: retry);
        }

        public JObject create(string resource, JObject payload, string query = "", bool retry = true)
        {
            return POST(path: "/" + version + "/" + resource, body: payload, query: query, retry: retry);
        }

        public JObject cancel(string resource, string id = "", string query = "", bool retry = true)
        {
            return DELETE(path: "/" + version + "/" + resource + "/" + id, query: query, retry: retry);
        }


        public class PostmenException : Exception
        {
            private int code;
            private Dictionary<string, string> details;
            private bool retryable;
            private JObject data;

            public int Code
            {
                get { return code; }
            }

            public Dictionary<string, string> Details
            {
                get { return details; }
            }

            public bool Retryable
            {
                get { return retryable; }
            }

            public JObject Raw
            {
                get { return data; }
            }

            public PostmenException(int code, Dictionary<string, string> details, bool retryable, string message, JObject data) : base(message)
            {
                this.code = code;
                this.details = details;
                this.retryable = retryable;
                this.data = data;

            }

            public static PostmenException FactoryMethod(JObject data)
            {
                int code_i;
                string message_i;
                Dictionary<string, string> details_i;
                bool retryable_i;
                JObject meta = (JObject)data["meta"];

                if (meta["code"] != null)
                {
                    code_i = (int)meta["code"];
                }
                else
                {
                    code_i = 0;
                }

                if (meta["message"] != null)
                {
                    message_i = (string)meta["message"];
                    if (code_i > 0)
                        message_i += " (" + code_i + ")";
                }
                else
                {
                    message_i = null;
                }

                if (meta["details"] != null)
                {
                    JArray details_ja = (JArray)meta["details"];
                    JObject aux;
                    int i;
                    details_i = new Dictionary<String, String>();
                    for (i = 0; i < details_ja.Count; i++)
                    {
                        aux = (JObject)details_ja[i];
                        if (!details_i.ContainsKey((string)aux["path"]))
                        {
                            details_i.Add((string)aux["path"], (string)aux["info"]);
                        }
                        else
                        {
                            details_i[(string)aux["path"]] += ", " + (string)aux["info"];
                        }
                        
                        //details_i.Add( (string)details_ja[i].Property<string(), (string) details_ja[i].Value<string>());
                        //details_i[i] = (string)details_ja[i];
                    }
                }
                else
                {
                    details_i = null;
                }

                if (meta["retryable"] != null)
                {
                    retryable_i = (bool)meta["retryable"];
                }
                else
                {
                    retryable_i = false;
                }

                return new PostmenException(code_i, details_i, retryable_i, message_i, data);
            }
        }
    }
}