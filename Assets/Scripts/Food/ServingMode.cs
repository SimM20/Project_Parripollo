/// <summary>
/// Defines how a meat cut can be served to the customer.
/// </summary>
public enum ServingMode
{
    /// <summary>Cut can only be served on a plate. No bread allowed.</summary>
    PlatedOnly,

    /// <summary>Cut can only be served in a sandwich. Must have bread.</summary>
    SandwichOnly,

    /// <summary>Cut can be served either plated or as a sandwich.</summary>
    Both
}
