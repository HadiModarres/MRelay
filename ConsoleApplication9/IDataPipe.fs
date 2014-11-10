module IDataPipe

[<AllowNullLiteral>]
type IDataPipe= // implemented by objects that receive and send data
    abstract TotalTransferedData: unit -> uint64 // return the count of the bytes that have been passed through this object
