using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
namespace ConsoleServer
{



    public class Server
    {
        class ChatMsg
        {
            public string msg;
            public string user;


            public ChatMsg()
            {

            }

            public static string OutMsg(string user, string msg)
            {
                return string.Format("User {0} says: {1}\r\n", user, msg);
            }

            public ChatMsg(string user, string msg)
            {
                this.user = user;
                this.msg = msg;
            }


            public string Msg
            {
                get
                {
                    return string.Format("User:{0}: {1}|\n", user, msg);
                }
            }
        }


        TcpListener tcpListener = null;
        ConcurrentDictionary<string, TcpClient> ClientSet = new ConcurrentDictionary<string, TcpClient>();
        IPAddress ServerIp = null;
        int port = 1234;
        ConcurrentQueue<string> msgQueue = new ConcurrentQueue<string>();
        public int Port
        {
            get
            {
                return port;
            }
            set
            {
                port = value;
            }
        }
        public Server()
        {
            ServerIp = IPAddress.Parse(getServerIP());
        }
        public Server(string serverIp)
        {
            ServerIp = IPAddress.Parse(serverIp);
        }
        public Server(IPAddress ipaddress)
        {
            ServerIp = ipaddress;
        }

        void sendAllClients(byte[] msgbte)
        {
            var Clients = ClientSet.Values.ToList();

            //Display in host
            Task.Run(new Action(() =>
            {
                Console.WriteLine(Encoding.UTF8.GetString(msgbte));
            }));
            //Send to client
            foreach (var client in Clients)
                if (client.Connected)
                    client.Client.Send(msgbte);


        }

        //void AcceptClient()
        //{
        //    TcpClient tmpTcpClient;
        //    while (true)
        //    {
        //        try
        //        {
        //            //建立與客戶端的連線
        //            tmpTcpClient = tcpListener.AcceptTcpClient();

        //            ClientSet[tmpTcpClient.Client.RemoteEndPoint.ToString()] = tmpTcpClient;

        //            if (tmpTcpClient.Connected)
        //            {
        //                Console.WriteLine("連線成功!");
        //                string msg = "server connection ok";

        //                var msgbte = Encoding.ASCII.GetBytes(msg);
        //                tmpTcpClient.Client.Send(msgbte);

        //                Task.Run(new Action(() =>
        //                {
        //                    while (true)
        //                    {
        //                        int bteNum = tmpTcpClient.Client.Receive(bteRecv);

        //                        Console.WriteLine(Encoding.UTF8.GetString(bteRecv, 0, bteNum));
        //                    }
        //                }));
        //                Task.Run(new Action(() =>
        //                {
        //                    while (true)
        //                    {
        //                        string str = Console.ReadLine();

        //                        tmpTcpClient.Client.Send(Encoding.UTF8.GetBytes(str));
        //                    }
        //                }));

        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            Console.WriteLine(ex.Message);
        //            Console.Read();
        //        }
        //    }
        //}

        void SendMsgQueueLoop()
        {
            Task.Run(() =>
            {
                while (true)
                {
                    string msgStr;
                    while (msgQueue.TryDequeue(out msgStr))
                        sendAllClients(Encoding.UTF8.GetBytes(msgStr));
                    System.Threading.SpinWait.SpinUntil(() => false, 1);
                }
            });
        }

        void hostChatLoop()
        {
            Task.Run(new Action(() =>
            {

                while (true)
                {
                    string str = Console.ReadLine();
                    str = ChatMsg.OutMsg("Host", str);

                    msgQueue.Enqueue(str);
                }
            }));
        }

        void ManageClientMsg(TcpClient MyTcpClient)
        {
            Task.Run(new Action(() =>
            {
                byte[] bteRecv = new byte[1024];
                while (true)
                {
                    int bteNum = MyTcpClient.Client.Receive(bteRecv);
                    string msgStr = Encoding.UTF8.GetString(bteRecv, 0, bteNum);
                    if (msgStr == "\r\n" || msgStr == "\n") //不讓換行洗版
                        continue;
                    msgStr = ChatMsg.OutMsg(MyTcpClient.Client.RemoteEndPoint.ToString(), msgStr);
                    msgQueue.Enqueue(msgStr);
                }
            }));
        }
        public void run()
        {
            SendMsgQueueLoop();
            hostChatLoop();
            tcpListener = new TcpListener(ServerIp, port);
            Console.WriteLine("Server ip:" + ServerIp.ToString());
            tcpListener.Start();
            Console.WriteLine("waiting for client connection");

            TcpClient tmpTcpClient;
            byte[] bteRecv = new byte[1024];
            while (true)
            {
                tmpTcpClient = tcpListener.AcceptTcpClient();
                ClientSet[tmpTcpClient.Client.RemoteEndPoint.ToString()] = tmpTcpClient; //Manage Client tcp conn by Concurrent dictionary
                if (tmpTcpClient.Connected) //Once connected, notfiy the client and start managing each client message
                {
                    Console.WriteLine(string.Format("=====client {0}  connected=====", tmpTcpClient.Client.RemoteEndPoint.ToString()));
                    var msgbte = Encoding.ASCII.GetBytes("server connection ok, Welcome to Moo's chatting room\r\n");
                    tmpTcpClient.Client.Send(msgbte);
                    ManageClientMsg(tmpTcpClient);
                }
            }


        }

        bool IsLocalIP(IPAddress ip)
        {
            string ipStr = ip.ToString();
            if (ipStr.Split('.')[0] != "192")
                return true;
            return false;
        }

        string getServerIP()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    if (IsLocalIP(ip))
                        return ip.ToString();
            throw new Exception("No external IPv4 address found in the system!");
        }


    }
    /// <summary>
    /// Server class
    /// </summary>
    public class Host
    {


        public static void Main()
        {
            Server server = new Server();
            server.run();

        }

    } // end class
} // end namesp
