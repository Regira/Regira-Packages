using Regira.Entities.Models.Abstractions;
using Regira.IO.Abstractions;

namespace Regira.Entities.Attachments.Abstractions;

public interface IAttachment : IBinaryFile, IHasTimestamps;
public interface IAttachment<TKey> : IAttachment, IEntity<TKey>;