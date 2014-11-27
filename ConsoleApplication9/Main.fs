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
    dynamicSegmentSize <- size

let processDynamicMinorBufferSize (size: int)=
    dynamicMinorBufferSize <- size

let processThrottleCount (count : int)=
    dynamicConnectionCount <- count

let processDynamic(dynamic: bool)=
    dynamicSupport <- dynamic
let processListenPort(port: int)=
    listenOnPort <- port

let processListenTcpCount(count: int)=
    listenTcpCount <- count

let processForwardAddress(address: string)=
    forwardAddress <- address

let processForwardPort(port: int)=
    forwardPort <- port

let processForwardTcpCount(count: int)=
    forwardTcpCount <- count

let processSegmentSize(size: int)=
    segmentSize <- size

let processMinorSocketBufferSize(size: int)=
    minorSocketBufferSize <- size

let processEncrypt(enc: bool)=
    encryptTraffic <- enc

let processIsMajorOnListenSide(majorListen: bool)=
    if majorListen = true then
        isMajorOnListenSide <- 0
    else 
        isMajorOnListenSide <- 1

let processHelp(stf: string)=
    help <- true
    Console.WriteLine ("")
    Console.WriteLine ("MRelay Help");
    Console.WriteLine ("")
    Console.WriteLine ("Usage: [mono] MRelay.exe [OPTIONS]+ -forwardAddress= -connectToRelay={true,false}");
    Console.WriteLine ("");
    Console.WriteLine ("OPTIONS:");
    p.WriteOptionDescriptions (Console.Out);    
    Console.WriteLine("\n Examples:\n")
    Console.Title <- "MRelay"
    
let processArgs(args: string[])=
    
    ignore(p.Add("listenOnPort|lp=", "The {PORT} to listen on (Default: 4000)",processListenPort))
    ignore(p.Add("listenTcpCount|ltc=", "The {COUNT} of incoming tcp connections (Default: 1)",processListenTcpCount))
    ignore(p.Add("relayToAddress|ra=", "The {ADDRESS} to relay incoming traffic to (Required)",processForwardAddress))
    ignore(p.Add("relayToPort|rp=", "The {PORT} to relay incoming traffic to (Default: 4000)",processForwardPort))
    ignore(p.Add("relayTcpCount|rtc=", "The {COUNT} of outgoing tcp connections (Default: 1)",processForwardTcpCount))
    ignore(p.Add("segmentSize|ss=", "The {SIZE} in bytes of each segment when dividing stream to segments (Default: 10000)",processSegmentSize))
    ignore(p.Add("socketBufferSize|sbs=", "The {SIZE} in bytes of buffer for each minor tcp socket (Default: 20000)",processMinorSocketBufferSize))
    ignore(p.Add("encrypt|e=", "whether relay should encrypt the data (Default: false)",processEncrypt))
    ignore(p.Add("help|h|?", "Show this message and exit",processHelp))
    ignore(p.Add("connectToRelay|cr=","Specify whether the other relay resides on listen or connect side of this relay.",processIsMajorOnListenSide))
    ignore(p.Add("dynamic|d=","enable or disable the on-demand throttling(Default: enabled)",processDynamic))
    ignore(p.Add("dynamicCount|dtc=","{NUMBER} of tcp connections to be made when throttling (Default: 10)",processThrottleCount))
    ignore(p.Add("dynamicSegmentSize|dss=","dynamic segment {SIZE} in bytes, this parameter only affects the tcp connections made when throttling (Default: 1024)",processDynamicSegmentSize))
    ignore(p.Add("dynamicBufferSize|dbs=","dynamic buffer {SIZE} in bytes, buffer size for each new socket when throttling (Default: 64*1024)",processDynamicMinorBufferSize))
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
    try
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
        else
            Console.WriteLine("Type \"MRelay.exe -h\" for help.")
     with
     | _ as e-> printfn "%A" e.Message;Console.WriteLine("Type \"MRelay.exe -h\" for help.")                      
    0

