///   2014 Sadegh Modarres   modarres.zadeh@gmail.com
/// 
///  This library is free software; you can redistribute it and/or
///  modify it under the terms of the GNU Lesser General Public
///  License as published by the Free Software Foundation; either
///  version 2.1 of the License, or (at your option) any later version.
/// 
///  This library is distributed in the hope that it will be useful,
///  but WITHOUT ANY WARRANTY; without even the implied warranty of
///  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
///  Lesser General Public License for more details.
module RelayTest

open Relay
open System.Net
open System.Net.Sockets
open System
open NDesk.Options
open EncryptedRelay
open phelix
open System.Threading



let mutable listenOnPort = 4000
let mutable listenTcpCount = 1
let mutable forwardAddress = null // required
let mutable forwardPort = 4000
let mutable forwardTcpCount = 1
let mutable segmentSize = 1024
let mutable minorSocketBufferSize = 1024
let mutable encryptTraffic = false
//let mutable decryptReceive = false
let mutable help = false
let mutable isMajorOnListenSide = -1


let mutable dynamicSegmentSize = 1024
let mutable dynamicMinorBufferSize= 1024*64
let mutable dynamicConnectionCount= 10
let mutable dynamicSupport=true


let p = new OptionSet()

let getAvilablePort() =
    let l = new TcpListener(IPAddress.Loopback,0)
    l.Start()
    let p = (l.LocalEndpoint :?> IPEndPoint).Port
    l.Stop()

    p

let processDynamicSegmentSize(size: int)=
    printfn "dynamic segment size: %i" size
    dynamicSegmentSize <- size

let processDynamicMinorBufferSize (size: int)=
    printfn "dynamic buffer size: %i" size
    dynamicMinorBufferSize <- size

let processThrottleCount (count : int)=
    printfn "dynamic tcp grow count: %i" count
    dynamicConnectionCount <- count

let processDynamic(dynamic: bool)=
    printfn "dynamic throttling enabled: %b" dynamic
    dynamicSupport <- dynamic
let processListenPort(port: int)=
    printfn "listen on port %i" port
    listenOnPort <- port

let processListenTcpCount(count: int)=
    printfn "listen tcp count %i" count
    listenTcpCount <- count

let processForwardAddress(address: string)=
    printfn "forward address: %A" address
    forwardAddress <- address

let processForwardPort(port: int)=
    printfn "forward port: %i" port
    forwardPort <- port

let processForwardTcpCount(count: int)=
    printfn "forward tcp count %i" count
    forwardTcpCount <- count

let processSegmentSize(size: int)=
    printfn "segment size: %i" size
    segmentSize <- size

let processMinorSocketBufferSize(size: int)=
    printfn "minor socket buffer size: %i" size
    minorSocketBufferSize <- size

//let processReadFakeRequest(read: bool)=
//    printfn "read fake request:  %b" read
//    readFakeRequest <- read
//
//let processSendFakeRequest(send: bool)=
//    printfn "send fake request %b" send
//    sendFakeRequest <- send

let processEncrypt(enc: bool)=
    printfn "encrypt data: %b" enc
    encryptTraffic <- enc

//let processDecryptReceive(decrypt: bool)=
//    printfn "decrypt receive: %b" decrypt
//    decryptReceive <- decrypt

let processIsMajorOnListenSide(majorListen: bool)=
    if majorListen = true then
        isMajorOnListenSide <- 0
    else 
        isMajorOnListenSide <- 1

let processHelp(stf: string)=
    help <- true
    Console.WriteLine ("Usage: relay [OPTIONS]+ -forwardAddress=");
    Console.WriteLine ("Relay tcp traffic through the forward address.");
    Console.WriteLine ("If no message is specified, a generic greeting is used.");
    Console.WriteLine ();
    Console.WriteLine ("Options:");
    p.WriteOptionDescriptions (Console.Out);    
    Console.WriteLine("\n Examples:\n")
    Console.WriteLine("You're working on computer A and have a Socks proxy running on port 1234 on computer B and you want to send the traffic over multiple tcp connections instead of 1 (what download accelerators do) then run: \n \"Relay.exe -listenOnPort=3500 -forwardToAddress=<AddressOfComputerB> -forwardToPort=6000 -forwardTcpCount=4\"  on A and\n \"Relay.exe -listenOnPort=6000 -forwardToAddress=127.0.0.1 -forwardToPort=1234 -listenTcpCount=4\" on B then set the browser proxy address on A to 127.0.0.1:3500" )
    Console.WriteLine("Above scenario but with encryption of traffic between A and B, run:\n \"Relay.exe -listenOnPort=3500 -forwardToAddress=<AddressOfComputerB> -forwardToPort=6000 -forwardTcpCount=4 -encryptReceive=true\" on A and \n \"Relay.exe -listenOnPort=6000 -forwardToAddress=127.0.0.1 -forwardToPort=1234 -listenTcpCount=4 -decryptReceive=true\" on B\n ")
    Console.WriteLine("Encrypt data between A and B and make it look like regular http traffic(to avoid slowdown of encrypted data transfer between A and B because of firewalls in between) run:\n \"Relay.exe -listenOnPort=3500 -forwardToAddress=<AddressOfComputerB> -forwardToPort=80 -encryptReceive=true -sendFakeRequest=true\" on A and \n \"Relay.exe -listenOnPort=80 -forwardToAddress=127.0.0.1 -forwardToPort=1234 -decryptReceive=true -receiveFakeRequest=true\" on B ")
    Console.WriteLine("Note: \n for relays to be able to work together they must have matching configurations for example segmentSize and minorSocketBufferSize must match and forwardTcpCount in Relay A must be same as listenTcpCount on relay B ")
    Console.WriteLine("The values of segmentSize and minorSocketBufferSize must be set according to the network bandwidth for optimal cpu usage and network transfer speed. When breaking a single tcp connection into multiple tcp connections every one of those connections is called a minorSocket. The value of minorSocketBufferSize must always be a multiple of segmentSize.")
let processArgs(args: string[])=
    
    ignore(p.Add("listenOnPort|lp=", "The {Port} to listen on (Default: 4000)",processListenPort))
    ignore(p.Add("listenTcpCount|ltc=", "The {Count} of incoming tcp connections (Default: 1)",processListenTcpCount))
    ignore(p.Add("forwardToAddress|fa=", "The {Address} to forward traffic to (Required)",processForwardAddress))
    ignore(p.Add("forwardToPort|fp=", "The {Port} to forward traffic to (Default: 4000)",processForwardPort))
    ignore(p.Add("forwardTcpCount|ftc=", "The {Count} of outgoing tcp connections (Default: 1)",processForwardTcpCount))
    ignore(p.Add("segmentSize|ss=", "The size in bytes of each segment when dividing stream to segments (Default: 10000)",processSegmentSize))
    ignore(p.Add("socketBufferSize|sbs=", "The size in bytes of buffer for each minor tcp socket (Default: 20000)",processMinorSocketBufferSize))
//    ignore(p.Add("readFakeRequest|rfr=", "Specify whether relay should read a fake http request when it accepts new tcp connections (Default: false)",processReadFakeRequest))
//    ignore(p.Add("sendFakeRequest|sfr=", "Specify whether relay should send a fake http request when it connects to the forward address (Default: false)",processSendFakeRequest))
    ignore(p.Add("encrypt|e=", "whether relay should encrypt the data (Default: false)",processEncrypt))
//    ignore(p.Add("decryptReceive|dr=", "whether relay should decrypt the data received from tcp connections received on listen port (Default: false)",processDecryptReceive))
    ignore(p.Add("help|h|?", "Show this message and exit",processHelp))
    ignore(p.Add("connectToRelay|cr=","Specify whether the other relay resides on listen or connect side of this relay. For example if your browser connects to this relay specify this flag as true and the flag of other relay as false.",processIsMajorOnListenSide))
    ignore(p.Add("dynamic|d=","enable or disable the on-demand throttling(Default: enabled)",processDynamic))
    ignore(p.Add("dynamicCount|dc=","number of tcp connections to be made when throttling (Default: 10)",processThrottleCount))
    ignore(p.Add("dynamicSegmentSize|dss=","dynamic segment size, this parameter only affects the tcp connections made when throttling",processDynamicSegmentSize))
    ignore(p.Add("dynamicBufferSize|dbs=","dynamic buffer size, buffer size for only the minor sockets when throttling",processDynamicMinorBufferSize))
    p.Parse(args)

    
let argValidity()=
    let mutable valid = true
    if listenTcpCount <> 1 && forwardTcpCount <> 1 then
        printfn "Bad configuration."
        valid <- false
    if listenTcpCount <= 0 || forwardTcpCount <= 0 then
        printfn "Bad configuration."
        valid <- false
    if forwardAddress = null then
        printfn "Bad configuration. forward address is not specified"
        valid <- false
    if forwardPort < 0 || forwardPort > 65536 then
        printfn "Bad configuration."
        valid <- false
    if segmentSize <=0 || minorSocketBufferSize <=0 then
        printfn "Bad configuration."
        valid <- false
    if minorSocketBufferSize % segmentSize <> 0 then
        printfn "Bad configuration. minor socket buffer size should be a multiple of segment size"
        valid <- false
    if dynamicSegmentSize <=0 || dynamicMinorBufferSize <= 0 then
        printfn "Bad configuration."
        valid <- false
    if dynamicMinorBufferSize % dynamicSegmentSize <> 0 then
        printfn "Bad configuration. minor socket buffer should be a multiple of segment size"
        valid <- false
    if dynamicConnectionCount <=0 then
        printfn "Bad configuraion, bad number for dynamic connection count"
        valid <- false
    if listenTcpCount=1 && forwardTcpCount=1 && isMajorOnListenSide= -1 then
        printfn "Missing Flag,specify the other relay direction using the -connectToRelay={true,false} flag"
        valid <- false
    
    try
        ignore(Dns.GetHostAddresses(forwardAddress).[0])
    with
    | e -> printfn "%A" e.Message; valid <- false

    valid

[<EntryPoint>]
   
let main argv = 
    ignore(processArgs(argv))
    if (help =false) && (argValidity() = true) then
        let isListenOnMajor = 
            match isMajorOnListenSide with
            | -1 when forwardTcpCount=1 -> false
            | -1 when listenTcpCount=1 -> true
            | 0 -> true
            | 1 -> false
            |_ -> false
            
        match encryptTraffic with
        | false ->  // multi relay only, no encryption        
            let r1 = new Relay(listenOnPort,listenTcpCount,Dns.GetHostAddresses(forwardAddress).[0],forwardPort,forwardTcpCount,segmentSize,minorSocketBufferSize,dynamicSegmentSize,dynamicMinorBufferSize,dynamicSupport,isListenOnMajor)
            let s= System.Console.ReadLine()
            ()
        | true when isListenOnMajor=true ->
            let freePort = getAvilablePort()
            let t1 = new Thread(fun () -> ignore(new EncryptedRelay(listenOnPort,Dns.GetHostAddresses("127.0.0.1").[0],freePort,true)))
            t1.Start()
            ignore(new Relay(freePort,listenTcpCount,Dns.GetHostAddresses(forwardAddress).[0],forwardPort,forwardTcpCount,segmentSize,minorSocketBufferSize,dynamicSegmentSize,dynamicMinorBufferSize,dynamicSupport,true))
//            ignore(new EncryptedRelay(listenOnPort,Dns.GetHostAddresses(forwardAddress).[0],forwardPort,true))
            ()
        | true when isListenOnMajor=false ->
            let freePort = getAvilablePort()
            let t1 = new Thread(fun () -> ignore(new Relay(listenOnPort,listenTcpCount,Dns.GetHostAddresses("127.0.0.1").[0],freePort,forwardTcpCount,segmentSize,minorSocketBufferSize,dynamicSegmentSize,dynamicMinorBufferSize,dynamicSupport,false)))
            t1.Start()
            ignore(new EncryptedRelay(freePort,Dns.GetHostAddresses(forwardAddress).[0],forwardPort,false))
    //        ignore(new EncryptedRelay(listenOnPort,Dns.GetHostAddresses(forwardAddress).[0],forwardPort,false))
            ()
        | _ -> ()

                           
    0

