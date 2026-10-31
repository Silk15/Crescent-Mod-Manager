namespace Crescent.Plugins
{
    public enum DeploymentState
    {
        NotDeployed,
        Deploying,
        Deployed,
        Withdrawing,
        PartiallyDeployed,
        Failed
    }
}