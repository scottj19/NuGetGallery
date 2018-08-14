using Autofac;

namespace NuGetGallery.Services
{
    public class BackgroundMessageServiceFactory : IBackgroundMessageServiceFactory
    {
        private readonly IComponentContext container;

        public BackgroundMessageServiceFactory(IComponentContext container)
        {
            this.container = container;
        }

        public BackgroundMessageService Create()
        {
            return container.Resolve<BackgroundMessageService>();
        }
    }
}