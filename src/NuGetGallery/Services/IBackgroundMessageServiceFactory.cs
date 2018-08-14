namespace NuGetGallery.Services
{
    public interface IBackgroundMessageServiceFactory
    {
        BackgroundMessageService Create();
    }
}