# Polymorphic Dispatch (Hierarchies)

EF Core Projectables supports abstract/virtual/overwritten properties and methods decorated with `[Projectable]`, and can generate expression trees to mimic virtual calls.

## Runtime

Virtual members are invoked for the most-specific type of the instance.

```csharp
public class Foo{
	public virtual string Name() => "Foo";
}

public class Bar : Foo{
	public override string Name() => "Bar";
}


Foo bar = new Bar();
bar.Name(); // "Bar"
```

## Expressions

Expressions are compiled and cannot know which type the provided instance will be, so the only solution is a type test chain, which gets automatically created.

```csharp
public class Foo{
	[Projectable(PolymorphicDispatch = true)]
	public virtual string Name() => "Foo";
	// Converted to: @this is Bar ? "Bar" : "Foo"
}

public class Bar : Foo{
	[Projectable(PolymorphicDispatch = true)]
	public override string Name() => "Bar";
	// Converted to: "Bar" as it has no derived types
}
```

## Abstract Members

Members can also be abstract, in which case the last branch of the type test chain will just be the last type itself.

```csharp
public abstract class Foo{
	[Projectable(PolymorphicDispatch = true)]
	public abstract string Name();
	// Converted to: @this is Bar ? "Bar" : "Baz"
}

public class Bar : Foo{
	[Projectable(PolymorphicDispatch = true)]
	public override string Name() => "Bar";
	// Converted to: "Bar" as it has no derived types
}

public class Baz : Foo{
	[Projectable(PolymorphicDispatch = true)]
	public override string Name() => "Baz";
	// Converted to: "Baz" as it has no derived types
}
```

## Base Invocations

You can also use base in your derived types to invoke the base method/property.

```csharp
public class Foo{
	[Projectable(PolymorphicDispatch = true)]
	public virtual string Name() => "Foo";
	// Converted to: @this is Bar ? (((Bar)@this).MyProp ? "Bar" : "Foo") : "Foo"
}

public class Bar : Foo{
	public bool MyProp { get; set; }

	[Projectable(PolymorphicDispatch = true)]
	public override string Name() => MyProp ? "Bar" : base.Name();
	// Converted to: @this.MyProp ? "Bar" : "Foo" as it has no derived types
}
```

## Enabling Polymorphic Dispatch

Add `PolymorphicDispatch = true` to the Projectables
