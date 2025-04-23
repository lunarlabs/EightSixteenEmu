using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace EmuXTesting
{
    public static class LabelParser
    {
        public static Dictionary<string, uint> ParseLabels(string listingFilePath, string mapFilePath)
        {
            var labels = new Dictionary<string, uint>();

            // Parse the segment list from the mapfile to get base addresses
            var segments = ParseSegments(mapFilePath);

            // Parse interruptTests.listing.txt
            var listingLines = File.ReadAllLines(listingFilePath);
            var listingRegex = new Regex(@"^([0-9A-Fa-f]{6})r\s+\d+\s+([a-zA-Z_][a-zA-Z0-9_]*)\s*:");
            var segmentRegex = new Regex(@"\.segment\s+""(\w+)""|\.code|\.rodata");

            string currentSegment = null;
            uint currentBaseAddress = 0;

            foreach (var line in listingLines)
            {
                // Check for segment definition
                var segmentMatch = segmentRegex.Match(line);
                if (segmentMatch.Success)
                {
                    // Determine the segment name
                    currentSegment = segmentMatch.Groups[1].Success ? segmentMatch.Groups[1].Value :
                                     line.Contains(".code") ? "CODE" :
                                     line.Contains(".rodata") ? "RODATA" : null;

                    // Update the base address for the current segment
                    if (currentSegment != null && segments.TryGetValue(currentSegment, out var segmentRange))
                    {
                        currentBaseAddress = segmentRange.Start;
                    }
                    continue;
                }

                // Check for label definition
                var labelMatch = listingRegex.Match(line);
                if (labelMatch.Success && currentSegment != null)
                {
                    var relativeAddress = Convert.ToUInt32(labelMatch.Groups[1].Value, 16);
                    var label = labelMatch.Groups[2].Value;

                    // Calculate the absolute address using the current base address
                    uint absoluteAddress = currentBaseAddress + relativeAddress;
                    labels[label] = absoluteAddress;
                }
            }

            return labels;
        }

        private static Dictionary<string, (uint Start, uint End)> ParseSegments(string mapFilePath)
        {
            var segments = new Dictionary<string, (uint Start, uint End)>();
            var mapLines = File.ReadAllLines(mapFilePath);
            var segmentRegex = new Regex(@"^(\w+)\s+([0-9A-Fa-f]{6})\s+([0-9A-Fa-f]{6})");

            foreach (var line in mapLines)
            {
                var match = segmentRegex.Match(line);
                if (match.Success)
                {
                    var segmentName = match.Groups[1].Value;
                    var start = Convert.ToUInt32(match.Groups[2].Value, 16);
                    var end = Convert.ToUInt32(match.Groups[3].Value, 16);
                    segments[segmentName] = (start, end);
                }
            }

            return segments;
        }

        private static uint AdjustAddress(uint relativeAddress, Dictionary<string, (uint Start, uint End)> segments)
        {
            foreach (var segment in segments)
            {
                var (start, end) = segment.Value;
                if (relativeAddress >= start && relativeAddress <= end)
                {
                    return start + (relativeAddress - start);
                }
            }

            throw new InvalidOperationException($"Relative address {relativeAddress:X} does not belong to any segment.");
        }
    }
}