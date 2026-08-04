using System.Runtime.Serialization;

namespace Regira.Invoicing.ViaAdValvas.Models;

internal enum GatewayLogType : int
{
    [EnumMember]
    Message = 0,

    [EnumMember]
    Warning = 1,

    [EnumMember]
    Error = 2,

    [EnumMember]
    Data = 3,

    [EnumMember]
    Stacktrace = 4,

    [EnumMember]
    Trace = 5,
}