module IPipeManager

open IDataPipe


[<AllowNullLiteral>]
type IPipeManager= // implemented by objects that receive and send data
    abstract needAConnection: obj -> unit // return the count of the bytes that have been passed through this object
    abstract getSegmentSize: unit -> int
    abstract getMinorSocketBufferSize: unit -> int
    abstract dataTransferIsAboutToBegin: IDataPipe -> unit
    abstract pipeDone: obj -> unit
