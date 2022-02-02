using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Mono.Data.Sqlite;

using System.Data;

namespace NetSignal
{
    public class MatchmakingConnectionUpdater
    {

        public struct ServerListElementResponse
        {
            public string name;
            public string ip;
            public int port;
            public int currentPlayerCount;
            public int maxPlayerCount;

            public override string ToString()
            {
                return name + "," + ip + ":" + port.ToString() + "," + currentPlayerCount.ToString() + "/" + maxPlayerCount.ToString();
            }
        }

        public struct ServerList
        {
            public List<ServerListElementResponse> list;

            public override string ToString()
            {
                string ret = "ServerList:[";
                foreach(var l in list)
                {
                    ret += l.ToString();
                    ret += ";";
                }
                ret += "]";
                return ret;

            }
        }

        public async static void AwaitAndPerformTearDownHttpListener(ConnectionAPIs connection, Func<bool> shouldTearDown, ConnectionState currentState)
        {

            while (!shouldTearDown())
            {
                await Task.Delay(1000);
            }
            /*while (currentState.udpStateName != StateOfConnection.ReadyToOperate)
            {
                await Task.Delay(1000);
            }*/
            Logging.Write("clean up http listener");

            //Util.Exchange(ref currentState.udpStateName, StateOfConnection.Uninitialized);

            connection.httpListener.Stop();
            connection.httpListener.Close();
        }

        public async static Task<ServerList> GatherServerList(ConnectionAPIs connection, ConnectionMetaData connectionData, ConnectionState connectionState)
        {

            //string uri = "http://127.0.0.1:" + httpPort + "/methodlist/" + selectedInspectable.id;
            string uri = connectionData.matchmakingServerIp + ":" + connectionData.matchmakingServerPort.ToString() + "/v0/matchmaking/serverlist";

            ServerList ret = new ServerList();
            ret.list = new List<ServerListElementResponse>();

            try
            {
                var getResponse = await connection.httpClient.GetAsync(uri);
                getResponse.EnsureSuccessStatusCode();
                string responseString = await getResponse.Content.ReadAsStringAsync();
                ret = Newtonsoft.Json.JsonConvert.DeserializeObject<ServerList>(responseString);
                Logging.Write(ret.ToString());
                                   
            } catch (Exception e)
            {
                Logging.Write(e);
            }
            return ret;
        }



        public async static void AwaitAndPerformTearDownHttpClient(ConnectionAPIs connection, Func<bool> shouldTearDown, ConnectionState currentState)
        {

            while (!shouldTearDown())
            {
                await Task.Delay(1000);
            }
            
            Logging.Write("clean up http client");

            connection.httpClient.Dispose();
            
        }

        public static void InitializeMatchMakingClient(ref ConnectionAPIs connection, ref ConnectionMetaData data, ref ConnectionState state, Func<bool> shouldTearDown)
        {
            connection.httpClient = new System.Net.Http.HttpClient();
            AwaitAndPerformTearDownHttpClient(connection, shouldTearDown, state);
            
        }

        public static void InitializeMatchMakingServer(ref ConnectionAPIs connection, ref ConnectionMetaData data, ref ConnectionState state, Func<bool> shouldTearDown)
        {
            try
            {

                Logging.Write("start and stop http listener");
                connection.httpListener = new HttpListener();
                //connection.httpListener.Prefixes.Add("http://+:" + 5042.ToString() + "/matchmaking/");
                connection.httpListener.Prefixes.Add("http://+:" + data.matchmakingServerPort.ToString() + "/v0/matchmaking/");
                connection.httpListener.Start();

                Util.Exchange(ref state.httpListenerStateName, StateOfConnection.ReadyToOperate);

                StartListenMatchmaking(connection, data, state, shouldTearDown);

                AwaitAndPerformTearDownHttpListener(connection, shouldTearDown, state);


            }
            catch (Exception e)
            {
                Logging.Write(e.Message);
                return;
            }
        }


        public static async void StartListenMatchmaking(ConnectionAPIs connection, ConnectionMetaData metaData, ConnectionState state, Func<bool> shouldStop)
        {
            Logging.Write("wait till listener ready");
            while ((StateOfConnection)state.httpListenerStateName != StateOfConnection.ReadyToOperate && !shouldStop())
            {
                await Task.Delay(100);
            }

            Logging.Write("get context");

            HttpListenerContext requestContext = null;
            try
            {
                requestContext = await connection.httpListener.GetContextAsync();
            }
            catch (ObjectDisposedException e)
            {
                Logging.Write("http listener was disposed, doesnt offer cancelation token, so this is intended");
            }
             catch(HttpListenerException e)
            {
                Logging.Write("http listener was disposed, doesnt offer cancelation token, so this is intended");
            }

            

            if (requestContext != null && (StateOfConnection)state.httpListenerStateName == StateOfConnection.ReadyToOperate && !shouldStop())
            {
                Logging.Write("got context " + requestContext + " " + (StateOfConnection)state.httpListenerStateName);
                Logging.Write("receive `?" + requestContext.Request.Url.LocalPath);

                StartListenMatchmaking(connection, metaData, state, shouldStop);

                

                HttpListenerRequest request = requestContext.Request;
                HttpListenerResponse response = requestContext.Response;

                Logging.Write("receive " + request.Url.LocalPath);

                if (request.HttpMethod.Equals("PUT") && request.Url.LocalPath.Equals("/v0/matchmaking/dedicatedkeepalive"))
                {
                    var body = request.InputStream;
                    var encoding = request.ContentEncoding;
                    var reader = new System.IO.StreamReader(body, encoding);
                    var data = await reader.ReadToEndAsync();
                    /*
                     * TODO json
                     * var execution = JsonUtility.FromJson<InspectionExecution>(data);
                    lock (inspectionExecutionLockObject)
                    {
                        requestedInspectionExecution.executionStart = execution.executionStart;
                        requestedInspectionExecution.withGameObjectName = execution.withGameObjectName;
                        requestedInspectionExecution.withPosition = execution.withPosition;
                    }
                    */
                    //Interlocked.Exchange<InspectionExecution>(ref requestedInspectionExecution, execution);
                    body.Close();
                    reader.Close();
                }

                if (request.HttpMethod.Equals("GET") && request.Url.LocalPath.Equals("/v0/matchmaking/serverlist"))
                {

                    var connectionPath = "data source=netsignal.db";
                    var con = new SqliteConnection(connectionPath);
                    Logging.Write("open db");
                    con.Open();
                    Logging.Write("opened db");
                    IDbCommand dbcmd;
                    IDataReader reader;
                    dbcmd = con.CreateCommand();

                    dbcmd.CommandText = "SELECT * FROM MatchList";
                    Logging.Write("execute sql");
                    reader = dbcmd.ExecuteReader();
                    Logging.Write("executed sql");

                    var serverList = new ServerList();
                    serverList.list = new List<ServerListElementResponse>();
                    while (reader.Read())
                    {

                        var serverEntry = new ServerListElementResponse();
                        serverEntry.name = reader.GetString(0);
                        serverEntry.currentPlayerCount = reader.GetInt32(1);
                        serverEntry.maxPlayerCount = reader.GetInt32(2);
                        serverEntry.ip = reader.GetString(3);
                        serverEntry.port = reader.GetInt32(4);

                        serverList.list.Add(serverEntry);
                    }

                    reader.Close();
                    con.Close();


                    var serializedServerList = Newtonsoft.Json.JsonConvert.SerializeObject(serverList, Newtonsoft.Json.Formatting.None);


                    var buffer = System.Text.Encoding.UTF8.GetBytes(serializedServerList);
                    response.ContentLength64 = buffer.Length;
                    using (System.IO.Stream outputStream = response.OutputStream)
                    {
                        await outputStream.WriteAsync(buffer, 0, buffer.Length);
                    }
                    response.Close();
                }
            }
        }
    }

}
