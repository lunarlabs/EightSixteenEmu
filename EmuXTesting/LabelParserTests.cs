using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace EmuXTesting
{
    public class LabelParserTests
    {
        [Fact]
        public void ParseLabels_ShouldHandleSegmentDefinitionsCorrectly()
        {
            // Arrange
            string listingFilePath = "test_interruptTests.listing.txt";
            string mapFilePath = "test_interruptTests.mapfile.txt";

            // Create mock listing file
            File.WriteAllText(listingFilePath, @"
000000r .code
000000r 1 start:
000010r 1 goNative:
000017r 1 enableIRQ:
00001Br 1 goWait:
000022r 1 nativenmi:
000026r 1 emunmi:
00002Ar 1 nativeirq:
00002Er 1 emuirq:
00002Er .segment ""RODATA""
000000r 1 magic:
");

            // Create mock map file
            File.WriteAllText(mapFilePath, @"
Segment list:
-------------
Name                   Start     End    Size  Align
----------------------------------------------------
CODE                  008000  008031  000032  00001
RODATA                008100  008101  000002  00001
HEADER                00FFD0  00FFFF  000030  00001
");

            // Act
            var labels = LabelParser.ParseLabels(listingFilePath, mapFilePath);

            // Assert
            var expectedLabels = new Dictionary<string, uint>
        {
            { "start", 0x008000 },
            { "goNative", 0x008010 },
            { "enableIRQ", 0x008017 },
            { "goWait", 0x00801B },
            { "nativenmi", 0x008022 },
            { "emunmi", 0x008026 },
            { "nativeirq", 0x00802A },
            { "emuirq", 0x00802E },
            { "magic", 0x008100 }
        };

            Assert.Equal(expectedLabels, labels);

            // Cleanup
            File.Delete(listingFilePath);
            File.Delete(mapFilePath);
        }
    }
}