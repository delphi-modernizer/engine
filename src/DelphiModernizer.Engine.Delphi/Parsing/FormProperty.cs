namespace DelphiModernizer.Engine.Delphi.Parsing;

/// <summary>A property value of a form component, with string lists joined by line feeds.</summary>
internal sealed record FormProperty(string Component, string ClassName, string Property, string Value, int Line);
