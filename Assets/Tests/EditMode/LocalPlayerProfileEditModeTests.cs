using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class LocalPlayerProfileEditModeTests
    {
        [Test]
        public void DuelistNameNormalizesWhitespaceAndKeepsAccents()
        {
            bool valid = Validate(
                "  Guardião   Arcano  ",
                out string normalized,
                out string rejection);

            Assert.That(valid, Is.True, rejection);
            Assert.That(normalized, Is.EqualTo("Guardião Arcano"));
        }

        [TestCase("")]
        [TestCase("AB")]
        [TestCase("Nome com símbolo!")]
        [TestCase("Este nome ultrapassa o limite profissional")]
        public void DuelistNameRejectsInvalidValues(string proposedName)
        {
            bool valid = Validate(
                proposedName,
                out _,
                out string rejection);

            Assert.That(valid, Is.False);
            Assert.That(rejection, Is.Not.Empty);
        }

        private static bool Validate(
            string proposedName,
            out string normalizedName,
            out string rejection)
        {
            Type repositoryType = AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly =>
                    assembly.GetType(
                        "ArcaneArena.Frontend.DeckRepository"))
                .First(type => type != null);
            MethodInfo validator = repositoryType.GetMethod(
                "TryValidatePlayerDisplayName",
                BindingFlags.Public | BindingFlags.Static);
            object[] arguments = { proposedName, null, null };
            bool valid = (bool)validator.Invoke(null, arguments);
            normalizedName = arguments[1] as string;
            rejection = arguments[2] as string;
            return valid;
        }
    }
}
