using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
namespace CompleteProject
{
    public class SocketManager : MonoBehaviour
    {
        private TcpClient socketConnection;
        private Thread clientReceiveThread;

        private const string HOST = "127.0.0.1";
        private const int PORT = 9051;

        public Agent _agent = null;
        void Awake()
        {
            ConnectToTcpServer();    
            //Time.timeScale = 0.0f;
            
            _agent.Init();
            _agent.AgentStart();
        }

        /// <summary> 	
        /// Setup socket connection. 	
        /// </summary> 	
        private void ConnectToTcpServer()
        {
            try
            {
                clientReceiveThread = new Thread(new ThreadStart(ListenForData));
                clientReceiveThread.IsBackground = true;
                clientReceiveThread.Start();
                _agent.Init();
                _agent.AgentStart();

            }
            catch (Exception e)
            {
                Debug.Log("On client connect exception " + e);
            }
        }
        /// <summary> 	
        /// Runs in background clientReceiveThread; Listens for incomming data. 	
        /// </summary>     
        private void ListenForData()
        {
            try
            {
                socketConnection = new TcpClient(HOST, PORT);
                Byte[] bytes = new Byte[1024];
                while (true)
                {
                    // Get a stream object for reading 				
                    using (NetworkStream stream = socketConnection.GetStream())
                    {
                        int length;
                        // Read incomming stream into byte arrary. 					
                        while ((length = stream.Read(bytes, 0, bytes.Length)) != 0)
                        {
                            var incommingData = new byte[length];
                            Array.Copy(bytes, 0, incommingData, 0, length);
                            // Convert byte array to string message. 						
                            string serverMessage = Encoding.UTF8.GetString(incommingData);
                            _agent.ListenForData(serverMessage);
                        }
                    }
                }
            }
            catch (SocketException socketException)
            {
                Debug.Log("Socket exception: " + socketException);
            }
        }
        /// <summary> 	
        /// Send message to server using socket connection. 	
        /// </summary> 	
        public void SendData(string msg)
        {
            if (socketConnection == null)
            {
                return;
            }
            try
            {
                // Get a stream object for writing. 			
                NetworkStream stream = socketConnection.GetStream();
                if (stream.CanWrite)
                {
                    // Convert string message to byte array.                 
                    byte[] clientMessageAsByteArray = Encoding.UTF8.GetBytes(msg);
                    // Write byte array to socketConnection stream.                 
                    stream.Write(clientMessageAsByteArray, 0, clientMessageAsByteArray.Length);
                }
            }
            catch (SocketException socketException)
            {
                Debug.Log("Socket exception: " + socketException);
            }
        }
    }
}