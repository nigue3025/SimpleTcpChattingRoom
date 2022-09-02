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
                {
                    try
                    {
                        client.Client.Send(msgbte);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                    }
                }

        }


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

        void hostChattingMode()
        {
            string str="";
            Console.WriteLine("host chatting mode starts.....");
            while(str!="exit")
            {
                str = Console.ReadLine();
                if (str == "exit")
                    break;
                 str = ChatMsg.OutMsg("Host", str);
                 msgQueue.Enqueue(str);
            }
            Console.WriteLine("host leaving chatting mode.....");

        }

        void displayClientsStatus()
        {
            if (ClientSet.Count == 0)
                Console.WriteLine("No clients found...");

            foreach(var client in ClientSet)
            {
                Console.WriteLine(string.Format("IP Port:{0} IsConneted:{1}", client.Key, client.Value.Client.Connected));
            }
        }

        void kickClient()
        {
            Console.WriteLine("key in the client ip for disconnection");
            var key = Console.ReadLine();
            if (ClientSet.ContainsKey(key))
                ClientSet[key].Client.Disconnect(true);
            else
                Console.WriteLine(string.Format("{0} not found. Unable to kick out such IP", key));

        }

        void kickAllClients()
        {
            foreach (var client in ClientSet)
                try
                {
                    if (client.Value.Client.Connected)
                        client.Value.Client.Disconnect(true);
                }
                catch(Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
        }
           

        void hostCtrlLoop()
        {
            
            Task.Run(new Action(() =>
            {

                while (true)
                {
                    string str = Console.ReadLine();
                    switch (str)
                    {
                        case "chat":
                            hostChattingMode();
                            break;
                        case "clients":
                            displayClientsStatus();
                            break;
                        case "kick":
                            kickClient();
                            break;
                        case "kickAll":
                            kickAllClients();
                            break;
                        default:
                            Console.WriteLine("Invalid command. Type chat, clients, kick or kickAll command");
                            break;
                    }
                  
                }
            }));
        }

        void ManageClientMsg(TcpClient MyTcpClient)
        {
            Task.Run(new Action(() =>
            {
                string msgStr="";
                try
                {
                    byte[] bteRecv = new byte[1024];
                    while (true)
                    {

                        int bteNum = MyTcpClient.Client.Receive(bteRecv);
                        msgStr = Encoding.UTF8.GetString(bteRecv, 0, bteNum);
                        if (msgStr == "\r\n" || msgStr == "\n" || msgStr==string.Empty) //不讓換行洗版
                            continue;
                        msgStr = ChatMsg.OutMsg(MyTcpClient.Client.RemoteEndPoint.ToString(), msgStr);
                        msgQueue.Enqueue(msgStr);
                    }
                }
                catch(System.Net.Sockets.SocketException exs)
                {
                    Console.WriteLine(string.Format("Socket exception:{1} \r\nClient IP {0} ", MyTcpClient.Client.RemoteEndPoint.ToString(), exs.SocketErrorCode));

                }
                catch (Exception ex)
                {
                    Console.WriteLine(string.Format("error message:{0}", msgStr));
                    Console.WriteLine(ex.ToString());
                }
            }));

        }
        public void run()
        {
            SendMsgQueueLoop();
            hostCtrlLoop();
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
                    string welcomeMsg = "server connection ok, Welcome to moo chatting room\r\n";
                    welcomeMsg = string.Format("{0}\r\nyour ip address:{1}\r\n",welcomeMsg, tmpTcpClient.Client.RemoteEndPoint.ToString());
                    var msgbte = Encoding.ASCII.GetBytes(welcomeMsg);

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
