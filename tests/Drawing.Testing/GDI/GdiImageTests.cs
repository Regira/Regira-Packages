using Drawing.Testing.Abstractions;
using Regira.Drawing.GDI.Services;

namespace Drawing.Testing.GDI;

public class GdiImageTests() : ImageTestsBase(new ImageService());