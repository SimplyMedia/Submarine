using Submarine.Core.Enums;

namespace Submarine.Infrastructure.Metadata;

/// <summary>
///     Resolves the consumer for a metadata consumer type.
/// </summary>
public interface IMetadataConsumerFactory
{
	/// <summary>
	///     Gets the consumer handling the given type.
	/// </summary>
	/// <exception cref="InvalidOperationException">No consumer is registered for the type.</exception>
	IMetadataConsumer Resolve(MetadataConsumerType type);
}

/// <summary>
///     Default factory picking from all registered consumers.
/// </summary>
public sealed class MetadataConsumerFactory(IEnumerable<IMetadataConsumer> consumers) : IMetadataConsumerFactory
{
	/// <inheritdoc />
	public IMetadataConsumer Resolve(MetadataConsumerType type)
		=> consumers.FirstOrDefault(consumer => consumer.Type == type)
			?? throw new InvalidOperationException($"No metadata consumer registered for {type}");
}
