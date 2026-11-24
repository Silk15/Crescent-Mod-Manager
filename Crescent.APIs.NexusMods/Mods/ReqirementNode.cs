namespace Crescent.APIs.NexusMods
{
    public class RequirementNode
    {
        public string NexusId { get; set; } = string.Empty;
        public string ModName { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public int Depth { get; set; }
        public bool IsExternal  { get; set; }
        public bool IsOptional  { get; set; }
        public bool IsSelected { get; set; }
        public bool IsInstalled { get; set; }
        
        public List<RequirementNode> Children { get; set; } = new();
    }
}