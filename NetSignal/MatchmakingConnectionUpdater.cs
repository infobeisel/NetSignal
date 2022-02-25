using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Mono.Data.Sqlite;

using System.Data;
using System.Net.Http;
using System.Threading;

namespace NetSignal
{
    public class MatchmakingConnectionUpdater
    {

        

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

            }
            catch (Exception e)
            {
                Logging.Write(e);
            }
            return ret;
        }

        public async static void PeriodicallySendKeepAlive(ConnectionAPIs connection, ConnectionMetaData connectionData, ConnectionState connectionState, ConnectionState [] clientStates, Func<bool> cancel, int periodMs, Action<string> report)
        {
            while (!cancel())
            {
                await UpdateServerEntry(connection, connectionData, connectionState, clientStates, report);
                await Task.Delay(periodMs);
            }
        }

        internal async static Task UpdateServerEntry(ConnectionAPIs connection, ConnectionMetaData connectionData, ConnectionState connectionState,ConnectionState [] clientStates, Action<string> report)
        {

            
            string uri = connectionData.matchmakingServerIp + ":" + connectionData.matchmakingServerPort.ToString() + "/v0/matchmaking/dedicatedkeepalive";

            ServerKeepAlive update = new ServerKeepAlive();
            update.ip = connectionData.myIp;
            update.port = connectionData.iListenToPort;

            int activeConCount = 0;
            foreach(var state in clientStates) {
                activeConCount += state.isConnectionActive ? 1 : 0;
            }
            Logging.Write("current players :" + activeConCount);

            update.currentPlayerCount = activeConCount;
            update.tick = DateTime.UtcNow.Ticks;

            try
            {
                var content = Newtonsoft.Json.JsonConvert.SerializeObject(update, Newtonsoft.Json.Formatting.None);

                var stringContent = new System.Net.Http.StringContent(content, System.Text.Encoding.UTF8, "application/json");

                Logging.Write("make update server entry");
                CancellationTokenSource source = new CancellationTokenSource();
                //previouslyProvidedToken = source.Token;
                _ = connection.httpClient.PostAsync(uri, stringContent, source.Token);
                await Task.Delay(2000);
                source.Cancel();


            }
            catch (HttpRequestException e)
            {
                Logging.Write("the matchmaking server is not available" ); 
            }
            finally
            {

            }
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

        public struct ServerKeepAlive
        {
            public string ip;
            public int port;
            public int currentPlayerCount;
            public long tick;
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

                if (request.HttpMethod.Equals("POST") && request.Url.LocalPath.Equals("/v0/matchmaking/dedicatedkeepalive"))
                {
                    var body = request.InputStream;
                    var encoding = request.ContentEncoding;
                    var reader = new System.IO.StreamReader(body, encoding);
                    var data = await reader.ReadToEndAsync();
                    var up = Newtonsoft.Json.JsonConvert.DeserializeObject<ServerKeepAlive>(data);

                    body.Close();
                    reader.Close();
                    response.ContentLength64 = 0;
                    response.Close();

                    MakeDatabaseUpdateOp(
                        "update MatchList set LastKeepAliveTick = " + up.tick + ", CurrentPlayerCount = " + up.currentPlayerCount +
                        " where Address =  '" + up.ip + "' and port = " + up.port + "");


                }

                if (request.HttpMethod.Equals("GET") && request.Url.LocalPath.Equals("/v0/matchmaking/serverlist"))
                {
                    var serverList = new ServerList();
                    serverList.list = new List<ServerListElementResponse>();
                    MakeDatabaseReadOp("SELECT * FROM MatchList", (IDataReader reader) =>
                    {
                        var serverEntry = new ServerListElementResponse();
                        serverEntry.name = reader.GetString(0);
                        serverEntry.currentPlayerCount = reader.GetInt32(1);
                        serverEntry.maxPlayerCount = reader.GetInt32(2);
                        serverEntry.ip = reader.GetString(3);
                        serverEntry.port = reader.GetInt32(4);
                        serverEntry.tick = long.Parse(reader.GetString(5));

                        serverList.list.Add(serverEntry);
                    });

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

        private static void MakeDatabaseReadOp(string sqlCommand, Action<IDataReader> resultReader)
        {
            var connectionPath = "data source=netsignal.db";
            var con = new SqliteConnection(connectionPath);
            IDataReader reader = null;
            Logging.Write("open db");
            try
            {
                con.Open();
            
                Logging.Write("opened db");
                IDbCommand dbcmd;
                
                dbcmd = con.CreateCommand();

                dbcmd.CommandText = sqlCommand;
                Logging.Write("execute sql");
                reader = dbcmd.ExecuteReader();
                Logging.Write("executed sql");


                while (reader.Read())
                {

                    resultReader(reader);
                }

                
            } catch (Exception e)
            {
                Logging.Write(e);
            } finally
            {
                reader.Close();
                con.Close();
            }
            
        }

        private static void MakeDatabaseUpdateOp(string sqlCommand)
        {
            Logging.Write(sqlCommand);
            var connectionPath = "data source=netsignal.db";
            var con = new SqliteConnection(connectionPath);

            IDbCommand dbcmd = null;
            Logging.Write("open db");
            try
            {
                con.Open();

                Logging.Write("opened db");

                dbcmd = con.CreateCommand();

                dbcmd.CommandText = sqlCommand;
                Logging.Write("execute sql");
                dbcmd.ExecuteNonQuery();
                Logging.Write("executed sql");
                
            }
            catch (Exception e)
            {
                Logging.Write(e);
            }
            finally
            {
                dbcmd.Dispose();
                con.Close();

            }

        }
    }

}
