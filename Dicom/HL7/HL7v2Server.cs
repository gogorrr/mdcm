using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Dicom.Network;

namespace Dicom.HL7 {
	/// <summary>
	/// Callback for received HL7v2 message.
	/// </summary>
	/// <param name="client">Connection to remote client</param>
	/// <param name="rawHL7Message">Received HL7v2 message</param>
	/// <param name="socket">Socket on which we operate</param>
	/// <returns>HL7 message to respond with or null if no response required</returns>
	public delegate string HL7v2MessageCallback(MLLP client, string rawHL7Message, DcmSocket socket);

	public class HL7v2Server {
		#region Private Members
		private List<int> _ports = new List<int>();
		private List<DcmSocketType> _types = new List<DcmSocketType>();
		private bool _stop;
		private List<Thread> _threads = new List<Thread>();
		private int _clientCount = 0;
		private Encoding _encoding = Encoding.Default;
		#endregion

		#region Public Constructor
		public HL7v2Server(Encoding encoding) {
			_encoding = encoding;
		}
		#endregion

		#region Public Properties
		public int ClientCount {
			get { return _clientCount; }
		}

		public HL7v2MessageCallback OnReceiveMessage;
		#endregion

		#region Public Methods
		public void AddPort(int port, DcmSocketType type) {
			_ports.Add(port);
			_types.Add(type);
		}

		public void ClearPorts() {
			_ports.Clear();
			_types.Clear();
		}

		public void Start() {
			if (_threads.Count == 0) {
				_stop = false;

				for (int i = 0; i < _ports.Count; i++) {
					try {
						DcmSocket socket = DcmSocket.Create(_types[i]);
						socket.Bind(new IPEndPoint(IPAddress.Any, _ports[i]));
						socket.Listen(5);

						Thread thread = new Thread(ServerProc);
						thread.IsBackground = true;
						thread.Start(socket);

						_threads.Add(thread);

						Dicom.Debug.Log.Info("HL7 {0} server listening on port {1}", _types[i].ToString(), _ports[i]);
					}
					catch (Exception ex) {
						Dicom.Debug.Log.Error("Unable to start HL7 {0} server on port {1}; Check to see that no other network services are using this port and try again.", _types[i].ToString(), _ports[i]);
						throw ex;
					}
				}
			}
		}

		public void Stop() {
			_stop = true;
			foreach (Thread thread in _threads) {
				thread.Join();
			}
			_threads.Clear();
			_clientCount = 0;
			Dicom.Debug.Log.Info("HL7 services stopped");
		}
		#endregion

		#region Private Members
		private void ServerProc(object state) {
			DcmSocket socket = (DcmSocket)state;
			
			try {
				while (!_stop) {
					if (socket.Poll(250000, SelectMode.SelectRead)) {
						DcmSocket client = socket.Accept();
						Thread thread = new Thread(ClientProc);
						thread.IsBackground = true;
						thread.Start(client);

						Debug.Log.Info("Client connecting from {0}", client.RemoteEndPoint);
					}
				}
			}
			finally {
				socket.Close();
			}
		}

		private void ClientProc(object state) {
			Interlocked.Increment(ref _clientCount);
			DcmSocket client = (DcmSocket)state;
			
			try {
				if (client.Type == DcmSocketType.TLS)
					Debug.Log.Info("Authenticating SSL/TLS for client: {0}", client.RemoteEndPoint);

				using (Stream stream = client.GetStream()) {
					MLLP mllp = new MLLP(stream, false, _encoding);

					while (true) {
						string rawIncomingMessage;
						if (!mllp.Receive(out rawIncomingMessage)) {
							break;
						}

						if (OnReceiveMessage != null) {
							string rawOutgoingMessage = OnReceiveMessage(mllp, rawIncomingMessage, client);
							if (rawOutgoingMessage != null) {
								mllp.Send(rawOutgoingMessage);
							}
						}
					}
				}
			}
			catch (Exception e) {
				Debug.Log.Error(e.Message);
			}
			finally {
				Interlocked.Decrement(ref _clientCount);
				client.Close();
			}
		}

		#endregion
	}
}
