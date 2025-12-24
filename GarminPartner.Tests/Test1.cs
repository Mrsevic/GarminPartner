using System.Linq;
namespace GarminPartner.Tests;


[TestClass]
public sealed class Test1
{
    [TestMethod]
    public void TestMethod1()
    {
        var prova = "adssda2133213asadas";
        var letters = string.Join("", prova.Where(v=> Char.IsAsciiLetter(v)));
        var lettersReverse = string.Join("", letters.Reverse());
        Console.WriteLine(letters);
    }
}