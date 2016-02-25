namespace MassEffect3.PackageUnpack
{
	internal interface IResource
	{
		string LocalName { get; }
		string FullName { get; }
		string LocalPath { get; }
		string FullPath { get; }
	}
}