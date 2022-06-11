using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PhuKienGiaReCrawler
{

    internal class Program
    {
        static string[] sources = new string[] { "https://phukiengiare.com/phu-kien-iphone.html",
                                             "https://phukiengiare.com/phu-kien-ipad.html",
                                            "https://phukiengiare.com/bao-da-op-lung-phu-kien-samsung.html"
        };

        static  void Main(string[] args)
        {
            ParallelLoopResult result = Parallel.For(0, sources.Length, RunTask);
        }

        public static void RunTask(int index)
        {
            HttpClient client = new HttpClient();
            HttpClient shopClient = new HttpClient();
            var baseUri = "https://phukiengiare.tino.page";
            var authHeader = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(ASCIIEncoding.ASCII.GetBytes("admin:admin")));
            shopClient.BaseAddress = new Uri(baseUri);
            
            // Phụ kiện

            IWebDriver browser = new ChromeDriver();
            browser.Navigate().GoToUrl(sources[index]);
            browser.FindElement(By.ClassName("more_load_page")).Click();
            Task.Delay(2000).Wait();
            browser.FindElement(By.ClassName("more_load_page")).Click();
            Task.Delay(2000).Wait();
            browser.FindElement(By.ClassName("more_load_page")).Click();
            Task.Delay(2000).Wait();

            // Lấy link sản phẩm 
            List<string> productLinks = new List<string>();
            var productCards = browser.FindElements(By.CssSelector(".mainbox-body .grid-list__item"));
            foreach (var productCard in productCards)
            {
                productLinks.Add(productCard.FindElement(By.ClassName("product-title")).GetAttribute("href"));
            }

            // Lấy thông tin từng sản phẩm
            var products = new List<Product>();
            var writer = new StreamWriter(String.Format("E:\\phukiengiare{0}.csv", index), false, System.Text.Encoding.UTF8);
            writer.WriteLine("Tên,Giá bán thường,Danh mục,Mô tả,Hình ảnh,Cân nặng (kg),Độ dài (cm),Độ rộng (cm),Chiều cao (cm),Link");

            for (int i = 0; i < productLinks.Count; i++)
            {

                browser.Navigate().GoToUrl(productLinks[i]);
                Product product = new Product();
                product.Link = productLinks[i];

                // Tên
                product.Name = browser.FindElement(By.CssSelector(".view_product_title h1.mainbox-title")).Text;

                //Giá bán thường
                product.Price = browser.FindElement(By.CssSelector(".price .price-num")).Text.Replace(".", "");

                // Danh mục
                var categories = browser.FindElements(By.CssSelector(".breadcrumbs bdi"));
                product.Category = categories[1].Text;
                for (int j = 1; j < categories.Count; j++)
                {
                    product.Category += ", " + categories[j].Text;
                }

                // Mô tả
                product.Description = browser.FindElement(By.CssSelector(".area_article")).GetAttribute("innerText").Replace("\"", "\"\"");


                // Ảnh
                var imgSrc = browser.FindElement(By.CssSelector("#thong_tin_san_pham .pict")).GetAttribute("src");
                var imgUri = new Uri(imgSrc);
                string fileName = Path.GetFileName(imgUri.LocalPath);
                string ext = Path.GetExtension(imgUri.LocalPath).Remove(0, 1);
                
                // tải ảnh về
                var response =  client.GetAsync(imgSrc).Result;

                // upload ảnh lên wordpress
                byte[] buffer =  response.Content.ReadAsByteArrayAsync().Result;
                var requestContent = new MultipartFormDataContent();
                var imageContent = new ByteArrayContent(buffer);
                imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse($"image/{ext}");
                requestContent.Add(imageContent, "file", fileName);
                requestContent.Add(new StringContent(product.Name), "title");

                var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/wp-json/wp/v2/media");
                requestMessage.Headers.Authorization = authHeader;
                requestMessage.Content = requestContent;

                var res = shopClient.SendAsync(requestMessage).Result;
                res.EnsureSuccessStatusCode();
                var jsonString = res.Content.ReadAsStringAsync().Result;
                dynamic data = JObject.Parse(jsonString);
                //var obj = JsonConvert.DeserializeObject<object>(jsonString);
                product.Image = (string)data.guid.raw;

                writer.WriteLine("\"{0}\",{1},\"{2}\",\"{3}\",{4},{5},{6},{7},{8},{9}", product.Name, product.Price, product.Category, product.Description,product.Image,1,1,1,1, product.Link);
                writer.Flush();

                Console.WriteLine("{0} - {1}", index, i);
                //Thread.Sleep(3000);
            }
            writer.Close();
        }
        static object GetPropertyValue(object obj, string propertyName)
        {
            var _propertyNames = propertyName.Split('.');

            for (var i = 0; i < _propertyNames.Length; i++)
            {
                if (obj != null)
                {
                    var _propertyInfo = obj.GetType().GetProperty(_propertyNames[i]);
                    if (_propertyInfo != null)
                        obj = _propertyInfo.GetValue(obj);
                    else
                        obj = null;
                }
            }

            return obj;
        }
    }
}

