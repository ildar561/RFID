using System;
using RFIDReaderAPI;
using RFIDReaderAPI.Interface;
using RFIDReaderAPI.Models;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace ReaderApp
{
    class Program : IAsynchronousMessage
    {
        public Program() { SetToken("PgjwTiNf9m7Y4VWSrrWMcllOPyt4YoNEdc3tSq5THRLMMd1nm1V3YcVO1Y2M"); SetUri("https://event-admin.tapir.ws/api/track-tag"); SetConnID("192.168.1.116:9090"); SetUpdateTime(100); } //Конструктор
//        public Program(string t) { SetToken(t); SetUri("https://event-admin.tapir.ws/api/track-tag"); SetConnID("192.168.1.116:9090"); SetUpdateTime(1000); SetDateTime("2020.05.08 8:00:00"); }
//        public Program(string t, string a) { SetToken(t); SetUri(a); SetConnID("192.168.1.116:9090"); SetUpdateTime(1000); SetDateTime("2020.05.08 8:00:00"); }
//        public Program(string t, string a, string b) { SetToken(t); SetUri(a); SetConnID(b); SetUpdateTime(1000); SetDateTime("2020.05.08 8:00:00"); }
//        public Program(string t, string a, string b, int c) { SetToken(t); SetUri(a); SetConnID(b); SetUpdateTime(c); SetDateTime("2020.05.08 8:00:00"); }
//        public Program(string t, string a, string b, int c, string d) { SetToken(t); SetUri(a); SetConnID(b); SetUpdateTime(c); SetDateTime(d); }

        private static string ConnID; //IP адресс устройства
        private static string Token;  //Токен
        private static int UpdateTime;//Частота считывания
        private static Uri uri;       //Адресс сервера

        static void Main(string[] args)
        {

            Program example = new Program(); //создаем переменную класса

            #region ConnectTCP
            while (true)
            {
                if (RFIDReader.CreateTcpConn(ConnID, example)) //подключение к устройству
                {
                    Console.WriteLine("Connect success!\n"); //успех
                    int rt = RFIDReader._RFIDConfig.SetANTPowerParam(ConnID, new Dictionary<int, int>() { { 1, 20 },{ 2, 20 } } ); //устанавливаем дальность считывания
                    if (rt == 0) Console.WriteLine("SET OK "); //успешно изменили дальность
                    else Console.WriteLine("SET FAILED "); //ошибка
                    break;
                }
                else
                {
                    Console.WriteLine("Connect failure!\n"); //подключение оборвалось
                    continue;
                }
            }
            #endregion

            try
            {
                RFIDReader._Tag6C.GetEPC(ConnID, eAntennaNo._1 | eAntennaNo._2, eReadType.Inventory); //производим считывание с антенн 1 и 2, тип потоковый
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message); //в случае прерываний, выводим сообщение с кодом ошибки
            }

            Console.ReadKey();
            RFIDReader._RFIDConfig.Stop(ConnID);//send stop instruction
            RFIDReader.CloseConn(ConnID); // close connection
        }

        #region interface implement
        
        public void SetConnID(string m)
        {
            ConnID = m;
        }
        
        public string GetConnID()
        {
            return ConnID;
        }
        
        public void SetUri(string m)
        {
            uri = new Uri(m);
        }
        
        public string GetUri()
        {
            return uri.ToString();
        }
        
        public string GetToken()
        {
            return Token;
        }
        
        public void SetToken(string m)
        {
            Token = m;
        }
        
        public void SetUpdateTime(int m)
        {
            UpdateTime = m;
            SetTagUpdateTime();
        }
        
        public int GetUpdateTime()
        {
            return UpdateTime;
        }
        
        public void SetTagUpdateTime()
        {
            RFIDReader._RFIDConfig.SetTagUpdateParam(ConnID, UpdateTime, 0);
        }
        
        public static void SetDateTime(string t)
        {
            RFIDReader._ReaderConfig.SetReaderUTC(ConnID, t);
        }
        
        public string GetDateTime()
        {
            return RFIDReader._ReaderConfig.GetReaderUTC(ConnID);
        }
        
        private string EPCtoUUID(string m) //перевод строки из EPC в UUID
        {
            m = m.ToLower();
            m = m.Substring(0, 8) + "-" + m.Substring(8, 4) + "-" + m.Substring(12, 4) + "-" + m.Substring(16, 4)
                + "-" + m.Substring(20, 12);
            return m;
        }

        private string ANT_NUM(int m) //переименовываем номер антенны из числа в строку
        {
            if(m == 1)
            {
                return "ANT1";
            }
            else
            {
                return "ANT2";
            }
        }

        public void OutPutTags(Tag_Model tag) //обработка вывода
        {
            if (tag.EPC.Length == 32) //проверка на длинну считываемого EPC
            { //если EPC подходит, шлем на сервер
                Template ReaderData = new Template(EPCtoUUID(tag.EPC), ANT_NUM(tag.ANT_NUM)); //создаем новый экземпляр класса Template
                string postData = JsonConvert.SerializeObject(ReaderData); //конвертируем сообщение в формат JSON
                Console.WriteLine(postData);
                byte[] bytes = Encoding.UTF8.GetBytes(postData); //создаем массив байтов
                var httpWebRequest = (HttpWebRequest)WebRequest.Create(uri); //подключаемся к серверу
                httpWebRequest.Headers.Add($"AccessToken: {GetToken()}");    //добавляем в header значение токена
                httpWebRequest.Method = "POST"; //устанавливаем метод POST
                httpWebRequest.ContentLength = bytes.Length; //устанавливаем длинну сообщения
                httpWebRequest.ContentType = "application/json"; //указываем тип
                using (Stream requestStream = httpWebRequest.GetRequestStream()) //создаем поток данных на сервер
                {
                    requestStream.Write(bytes, 0, bytes.Count()); //побайтово шлем информацию
                }
                var httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse(); //получаем ответ от сервера
                if (httpWebResponse.StatusCode != HttpStatusCode.OK) //если возникли проблемы, то выводит сообщение с кодом ошибки
                {
                    string message = String.Format("POST failed. Received HTTP {0}", httpWebResponse.StatusCode);
                    throw new ApplicationException(message);
                }
            }
        }
        public void WriteDebugMsg(string msg)
        { }
        public void WriteLog(string msg)
        { }
        public void PortClosing(string connID)
        { }
        public void OutPutTagsOver()
        { }
        public void PortConnecting(string connID)
        { }
        public void GPIControlMsg(GPI_Model gpi_model)
        { }
        #endregion
    }
    class Template //класс, необходимый для корректного формирования сообщения в формате JSON
    {
        public Template(string a, string b) { epc = a; reader = "A1"; antenna = b; timestamp = GetTimeStamp(); }
        public string epc { get; set; }
        public string reader { get; set; }
        public string antenna { get; set; }
        public int timestamp { get; set; }
        public int GetTimeStamp() //получение текущего времени в формате UTC
        {
            return (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            //return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }

}