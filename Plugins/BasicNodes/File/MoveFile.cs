namespace FileFlow.BasicNodes.File
{
    using System.ComponentModel;
    using FileFlow.Plugin;
    using FileFlow.Plugin.Attributes;

    public class MoveFile : Node
    {
        public override int Outputs => 1;
        public override FlowElementType Type => FlowElementType.Process;
    }
}