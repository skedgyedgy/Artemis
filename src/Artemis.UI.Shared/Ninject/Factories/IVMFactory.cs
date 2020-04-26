﻿using Artemis.Core.Models.Profile;
using Artemis.Core.Models.Profile.Colors;
using Artemis.UI.Shared.Screens.GradientEditor;

namespace Artemis.UI.Shared.Ninject.Factories
{
    public interface IVmFactory
    {
    }

    public interface IGradientEditorVmFactory : IVmFactory
    {
        GradientEditorViewModel Create(ColorGradient colorGradient);
    }
}