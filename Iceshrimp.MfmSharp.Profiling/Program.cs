using Iceshrimp.MfmSharp;
using Iceshrimp.MfmSharp.Examples;

GC.Collect();
MfmParser.Parse(MfmExamples.UnmatchedBoldNodeManyNested());
GC.Collect();