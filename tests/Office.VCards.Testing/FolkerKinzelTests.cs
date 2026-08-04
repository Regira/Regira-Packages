using Office.VCards.Testing.Abstractions;
using Regira.Office.VCards.FolkerKinzel;

namespace Office.VCards.Testing;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class FolkerKinzelTests() : VCardsTestsBase(new VCardManager());