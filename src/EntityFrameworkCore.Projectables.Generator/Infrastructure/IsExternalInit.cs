// ReSharper disable CheckNamespace

// Polyfill for C# 9 record types when targeting netstandard2.0 or netstandard2.1
// The compiler requires this type to exist in order to use init-only setters (used by records).
namespace System.Runtime.CompilerServices;

sealed internal class IsExternalInit { }