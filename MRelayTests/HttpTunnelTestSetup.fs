module HttpTunnelTestSetup

open System
open System.Security.Cryptography
open System.Resources
open System.IO
open System
open System.Net
open System.Net.Sockets
open System.Threading
open System.Collections
open SocketStore
open Relay
open EncryptedRelay
open Microsoft.VisualStudio.TestTools.UnitTesting   
open IDataPath
open HttpTunnelRelay

type TestHttpTunnelSetup(entranceIP:IPAddress,entrancePort: int, exitIP:IPAddress,exitPort: int) as x=
//    let entranceIP = Dns.GetHostAddresses("127.0.0.1").[0]
//    let entrancePort = 8000
//    let exitIP= Dns.GetHostAddresses("127.0.0.1").[0]
//    let exitPort=9000
    let getAvilablePort() =
        let l = new TcpListener(IPAddress.Loopback,0)
        l.Start()
        let p = (l.LocalEndpoint :?> IPEndPoint).Port
        l.Stop()
    
        p

    do 
        x.SetupEncryptDecryptRelays()

    
    member x.SetupEncryptDecryptRelays()=
             let freeport = getAvilablePort()         
             let t1 = new Thread( fun () -> ignore(new HttpTunnelRelay(entrancePort,Dns.GetHostAddresses("127.0.0.1").[0],freeport,true)))
             let t2 = new Thread( fun () -> ignore(new HttpTunnelRelay(freeport,Dns.GetHostAddresses("127.0.0.1").[0],exitPort,false)))
             t1.Start()
             t2.Start()

    interface IDataPath with
        member x.entranceIP() =
            entranceIP
        member x.entrancePort() =
            entrancePort
        member x.exitIP()=
            exitIP
        member x.exitPort()=
            exitPort
            
