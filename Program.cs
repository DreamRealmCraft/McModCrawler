using System;
using System.Net;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace McModCrawler
{
    class Program
    {
        const int size = 10;//mcmod里mod的数量，目前不到10000个，实际爬设置为10000即可
        struct Format {
        public string name;//名称
        public int mcmodid;//mcmod编号,-1为不存在
        public string cfid;//curseforge id
        public string mdid;//modrinth id
        }
        static void Main(string[] args)
        {
            int i = ReadFromFile();
            Console.WriteLine("Format: 中文mod名|mcmod编号|curseforge id|modrinth id");
           GetUrls(i);
        }



        static void GetUrls(int start)
        {
            string url = "https://www.mcmod.cn/class/";
            Dictionary<int, string> modmap = new Dictionary<int, string>();//创建一个包含i以及mod名称的字典
            for(int i = start+1; i <=size; i++)
            {
                string modurl = url + i + ".html";// example: https://www.mcmod.cn/class/1.html
                Format name = crawl(modurl,i);
                if (name.mcmodid == -1) {
                    continue;
                }
                WriteToFile(name);
            }
            File.AppendAllText("modlist.json", "]");
            return;
        }
        static Format crawl(string url,int i)
        {
            Format output = new Format();
            string pattern1 = @"<h3>(.*?)<\/h3>";//获取中文名
            string pattern2 = @"<h4>(.*?)<\/h4>";//获取英文名（如果有）
            string curseforge = @"""CurseForge""  target=""_blank"" rel=""nofollow noreferrer"" target=""_blank"" href=""(.*?)"">";//获取地址
            string modrinth = @"""Modrinth""  target=""_blank"" rel=""nofollow noreferrer"" target=""_blank"" href=""(.*?)"">";
            string result1 = null;//中文名
            string result2 = null;//英文名
            string resultaddress = null;//地址
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.UserAgent = "User-Agent,Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/111.0.0.0 Safari/537.36";
            request.Timeout = 3000;
            try
            {
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    output.mcmodid = i;
                    Stream stream = response.GetResponseStream();
                    StreamReader reader = new StreamReader(stream, Encoding.UTF8);
                    string content = reader.ReadToEnd();

                    //获取中文名
                    Regex regex = new Regex(pattern1);
                    Match match = regex.Match(content);
                    if (match.Success)
                    {
                        result1 = match.Groups[1].Value;
                    }


                    //获取英文名（如果有）
                    Regex regex2 = new Regex(pattern2);
                    Match match2 = regex2.Match(content);
                    if (match2.Success)
                    {
                        result2 = match2.Groups[1].Value;
                    }

                    //获取CF外链
                    Regex rcurseforge = new Regex(curseforge);
                    Match mcurseforge = rcurseforge.Match(content);
                    if (mcurseforge.Success)
                    {
                        resultaddress = mcurseforge.Groups[1].Value;
                        string inlink = "https:" + resultaddress;
                        string cfid = GetCFid(inlink);
                        output.cfid = cfid;
                    }
                    //获取MD外链
                    Regex rmodrinth = new Regex(modrinth);
                    Match mmodrinth = rmodrinth.Match(content);
                    if (mmodrinth.Success)
                    {
                        resultaddress = mmodrinth.Groups[1].Value;
                        string inlink = "https:" + resultaddress;
                        string mdid = GetMDid(inlink);
                        output.mdid = mdid;
                    }
                }
            }
            catch (WebException ex)
            {
                //Console.WriteLine("网站不存在，原因：" + ex.Message);
                return new Format(){mcmodid = -1};
            }
            if (result1 != null && result2 != null) {
                output.name = result1 + " " + result2;
            }else {
                output.name = result1;
            }
            

            return output;
        }
        private static void WriteToFile(Format f)//写入json
        {
            string json = JsonConvert.SerializeObject(f);
            Console.WriteLine(json);
            File.AppendAllText("modlist.json",  json + ",\n");
        }

        private static int ReadFromFile()//读取json,找到最后一个mcmodid
        {
            string json = File.ReadAllText("modlist.json");
            List<Format> f = JsonConvert.DeserializeObject<List<Format>>(json);
            json = json.Replace("]","");
            Console.WriteLine(json);
            File.WriteAllBytes("modlist.json", Encoding.UTF8.GetBytes(json));
            return f[f.Count - 1].mcmodid;
        }


        private static string GetCFid(string url)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.UserAgent = "Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/66.0.3359.181 Safari/537.36";
            request.Timeout = 15000;
            request.AllowAutoRedirect = false;

            HttpWebResponse response = (HttpWebResponse)request.GetResponse();

            if (response.StatusCode == HttpStatusCode.Redirect || 
                response.StatusCode == HttpStatusCode.MovedPermanently)
            {
                //获取CF project id
                string redirectUrl = response.Headers["Location"];//CF外链
                redirectUrl = redirectUrl.Replace("https://www.curseforge.com/minecraft/mc-mods/", "");//CFmod名称，从外链截取
                return CFSearchMods(redirectUrl);//使用CF API查找modid

            }
            return null;
        }

        private static string GetMDid(string url)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.UserAgent = "Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/66.0.3359.181 Safari/537.36";
            request.Timeout = 15000;
            request.AllowAutoRedirect = false;

            HttpWebResponse response = (HttpWebResponse)request.GetResponse();

            if (response.StatusCode == HttpStatusCode.Redirect || 
                response.StatusCode == HttpStatusCode.MovedPermanently)
            {
                //获取CF project id
                string redirectUrl = response.Headers["Location"];//MD外链
                redirectUrl = redirectUrl.Replace("https://modrinth.com/mod/", "");//MDmod名称，从外链截取
                return MDSearchMods(redirectUrl);//使用MD API查找modid

            }
            return null;
        }

        private const string APIPrefix = "https://api.curseforge.com";
        private const string APIXKey = "$2a$10$o8pygPrhvKBHuuh5imL2W.LCNFhB15zBYAExXx/TqTx/Zp5px2lxu";
        private const string MinecraftGameId = "432";

        public static string CFSearchMods(string query)
        {
            string pattern = @"modId"":(.*?),""";
            string result = null;//存储modid的变量
            HttpClient httpClient = new();
            string url = "https://api.curseforge.com/v1/mods/search?gameId=432&searchFilter="+query+"&classId=6&index=0&sortOrder=desc";
            HttpRequestMessage request = new(HttpMethod.Get, url);
            request.Headers.Add("Accept", "application/json");
            request.Headers.Add("x-api-key", APIXKey);
            var response = httpClient.Send(request);
            response.EnsureSuccessStatusCode();
            Stream myResponseStream = response.Content.ReadAsStream();
            StreamReader myStreamReader = new(myResponseStream, Encoding.UTF8);
            string retString = myStreamReader.ReadToEnd();
            Regex regex = new Regex(pattern);
            Match match = regex.Match(retString);
            if (match.Success)
            {
                result = match.Groups[1].Value;
            }
            myResponseStream.Close();
            myStreamReader.Close();
            return result;
        }
        public static string MDSearchMods(string query)
        {
            string pattern = @"project_id"":""(.*?)""";
            string result = null;//存储modid的变量
            HttpClient httpClient = new();
            string url = "https://api.modrinth.com/v2/search?query="+query+"&limit=20&index=relevance";
            HttpRequestMessage request = new(HttpMethod.Get, url);
            request.Headers.Add("Accept", "application/json");
            request.Headers.Add("x-api-key", APIXKey);
            var response = httpClient.Send(request);
            response.EnsureSuccessStatusCode();
            Stream myResponseStream = response.Content.ReadAsStream();
            StreamReader myStreamReader = new(myResponseStream, Encoding.UTF8);
            string retString = myStreamReader.ReadToEnd();
            Regex regex = new Regex(pattern);
            Match match = regex.Match(retString);
            if (match.Success)
            {
                result = match.Groups[1].Value;
            }
            myResponseStream.Close();
            myStreamReader.Close();
            return result;
        }
    }
}
