using System.Runtime.CompilerServices;
using Regira.Entities.Web;
using Regira.Entities.Web.Models;
using Regira.Entities.Web.Models.Abstractions;

// The response envelopes and the save helper live in Regira.Entities.Mediator, whose handlers build them, under the
// namespaces they had here: code compiled against an earlier Regira.Entities.Web still finds them through these.
[assembly: TypeForwardedTo(typeof(IEntityResult))]
[assembly: TypeForwardedTo(typeof(DetailsResult<>))]
[assembly: TypeForwardedTo(typeof(ListResult<>))]
[assembly: TypeForwardedTo(typeof(SearchResult<>))]
[assembly: TypeForwardedTo(typeof(SaveResult<>))]
[assembly: TypeForwardedTo(typeof(DeleteResult<>))]
[assembly: TypeForwardedTo(typeof(CountResult))]
[assembly: TypeForwardedTo(typeof(EntitySaveHelper))]
