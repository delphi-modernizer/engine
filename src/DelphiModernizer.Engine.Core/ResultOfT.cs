using System;
using System.Diagnostics.CodeAnalysis;

namespace DelphiModernizer.Engine.Core;

/// <summary>The outcome of an operation that returns a <typeparamref name="TValue"/> on success.</summary>
/// <typeparam name="TValue">The type produced when the operation succeeds.</typeparam>
public sealed class Result<TValue> : Result
{
    internal Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error)
    {
        Value = value;
    }

    [AllowNull]
    public TValue Value
    {
        get {
            if (IsFailure)
            {
                throw new InvalidOperationException("The value of a failed result cannot be accessed.");
            }

            return field!;
        }
    }

    public static implicit operator Result<TValue>(TValue value)
    {
        return Success(value);
    }
}
