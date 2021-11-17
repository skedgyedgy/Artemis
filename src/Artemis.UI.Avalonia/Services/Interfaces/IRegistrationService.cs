﻿namespace Artemis.UI.Avalonia.Services.Interfaces
{
    public interface IRegistrationService : IArtemisUIService
    {
        void RegisterBuiltInDataModelDisplays();
        void RegisterBuiltInDataModelInputs();
        void RegisterBuiltInPropertyEditors();
        void RegisterProviders();
        void RegisterControllers();
        void ApplyPreferredGraphicsContext();
    }
}