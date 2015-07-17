module ReliabilityTests

open IDataPath
open System
open Microsoft.VisualStudio.TestTools.UnitTesting
open System.Security.Cryptography
open System.Resources
open System.IO
open System
open System.Net
open System.Net.Sockets
open System.Threading
open System.Collections
open System.Collections.Generic
open SocketStore
open Relay
open Client
open Server
open DataDelegate
open HttpTunnelTestSetup

[<TestClass>]
type ReliabilityTest() as x=
    let set = new HashSet<string>()
    let all = 1
    let mutable count = all
    let doneobj= new obj()
    let lockobj = new obj()
    let reset = new AutoResetEvent(false)
    let dataSize = 1024*1024*256


    [<TestMethod>]
    member x.TestHttpTunnelReliability()=
        let k = new DataServer(9000,dataSize,x)
        let t2 = new Thread(fun () -> ignore(k.StartListening()))
        t2.Start()
        
        let te = new TestHttpTunnelSetup(Dns.GetHostAddresses("127.0.0.1").[0],8000,Dns.GetHostAddresses("127.0.0.1").[0],9000)
        x.TestDataPath(te)
    member x.TestDataPath(path: IDataPath)=
        Monitor.Enter doneobj
       
        for i=0 to all-1 do
            let cli = new DataClient(dataSize,path.entranceIP(),path.entrancePort(),x)
            let t1 = new Thread(fun () -> cli.Send())
            t1.Start()
//        let serv = new DataServer(path.exitPort(),10*1024*1024,x)
//        serv.StartListening()
//        
        reset.WaitOne()
       // Monitor.Wait(doneobj)
        x.Done()
    member x.Done()=
       // Monitor.Enter doneobj
        Assert.AreEqual(1,1)
    interface IDataDelegate with
        member x.ReceivedDataHash(hash: byte[])=
            Monitor.Enter lockobj            
       //     let h = set.Contains(hash)
            let s = BitConverter.ToString(hash)
            if set.Contains(s) = false then
                Assert.Fail("malformed data")
                ignore(reset.Set())

            else
                count <- (count-1)
                if count=0 then
                    ignore(reset.Set())
            Monitor.Exit lockobj
        member x.SentDataHash(hash:byte[])=
            Monitor.Enter lockobj
            ignore(set.Add(BitConverter.ToString(hash)))
            Monitor.Exit lockobj



        
    