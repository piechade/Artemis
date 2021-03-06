﻿using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Artemis.Core;
using Artemis.UI.Extensions;
using Artemis.UI.Screens.Shared;
using Artemis.UI.Services;
using Artemis.UI.Shared.Services;

namespace Artemis.UI.Screens.ProfileEditor.Visualization
{
    public class ProfileLayerViewModel : CanvasViewModel
    {
        private readonly ILayerEditorService _layerEditorService;
        private readonly PanZoomViewModel _panZoomViewModel;
        private readonly IProfileEditorService _profileEditorService;
        private bool _isSelected;
        private Geometry _shapeGeometry;
        private Rect _viewportRectangle;

        public ProfileLayerViewModel(Layer layer, PanZoomViewModel panZoomViewModel, IProfileEditorService profileEditorService, ILayerEditorService layerEditorService)
        {
            _panZoomViewModel = panZoomViewModel;
            _profileEditorService = profileEditorService;
            _layerEditorService = layerEditorService;
            Layer = layer;
        }

        public Layer Layer { get; }

        public Geometry ShapeGeometry
        {
            get => _shapeGeometry;
            set => SetAndNotify(ref _shapeGeometry, value);
        }

        public Rect ViewportRectangle
        {
            get => _viewportRectangle;
            set
            {
                if (!SetAndNotify(ref _viewportRectangle, value)) return;
                NotifyOfPropertyChange(nameof(LayerPosition));
            }
        }

        public double StrokeThickness
        {
            get
            {
                if (IsSelected)
                    return Math.Max(2 / _panZoomViewModel.Zoom, 1);
                return Math.Max(2 / _panZoomViewModel.Zoom, 1) / 2;
            }
        }

        public double LayerStrokeThickness
        {
            get
            {
                if (IsSelected)
                    return StrokeThickness / 2;
                return StrokeThickness;
            }
        }

        public Thickness LayerPosition => new(ViewportRectangle.Left, ViewportRectangle.Top, 0, 0);

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (!SetAndNotify(ref _isSelected, value)) return;
                NotifyOfPropertyChange(nameof(StrokeThickness));
            }
        }

        private void PanZoomViewModelOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(_panZoomViewModel.Zoom))
            {
                NotifyOfPropertyChange(nameof(StrokeThickness));
                NotifyOfPropertyChange(nameof(LayerStrokeThickness));
            }
        }

        private void Update()
        {
            IsSelected = _profileEditorService.SelectedProfileElement == Layer;

            CreateShapeGeometry();
            CreateViewportRectangle();
        }

        private void CreateShapeGeometry()
        {
            if (Layer.LayerShape == null || !Layer.Leds.Any())
            {
                ShapeGeometry = Geometry.Empty;
                return;
            }

            Rect bounds = _layerEditorService.GetLayerBounds(Layer);
            Geometry shapeGeometry = Geometry.Empty;
            switch (Layer.LayerShape)
            {
                case EllipseShape _:
                    shapeGeometry = new EllipseGeometry(bounds);
                    break;
                case RectangleShape _:
                    shapeGeometry = new RectangleGeometry(bounds);
                    break;
            }

            if (Layer.LayerBrush == null || Layer.LayerBrush.SupportsTransformation)
                shapeGeometry.Transform = _layerEditorService.GetLayerTransformGroup(Layer);

            ShapeGeometry = shapeGeometry;
        }

        private void CreateViewportRectangle()
        {
            if (!Layer.Leds.Any() || Layer.LayerShape == null)
            {
                ViewportRectangle = Rect.Empty;
                return;
            }

            ViewportRectangle = _layerEditorService.GetLayerBounds(Layer);
        }

        private Geometry CreateRectangleGeometry(ArtemisLed led)
        {
            Rect rect = led.RgbLed.AbsoluteBoundary.ToWindowsRect(1);
            return new RectangleGeometry(rect);
        }

        private Geometry CreateCircleGeometry(ArtemisLed led)
        {
            Rect rect = led.RgbLed.AbsoluteBoundary.ToWindowsRect(1);
            return new EllipseGeometry(rect);
        }

        private Geometry CreateCustomGeometry(ArtemisLed led, double deflateAmount)
        {
            Rect rect = led.RgbLed.AbsoluteBoundary.ToWindowsRect(1);
            try
            {
                PathGeometry geometry = Geometry.Combine(
                    Geometry.Empty,
                    Geometry.Parse(led.RgbLed.ShapeData),
                    GeometryCombineMode.Union,
                    new TransformGroup
                    {
                        Children = new TransformCollection
                        {
                            new ScaleTransform(rect.Width, rect.Height),
                            new TranslateTransform(rect.X, rect.Y)
                        }
                    }
                );

                return geometry;
            }
            catch (Exception)
            {
                return CreateRectangleGeometry(led);
            }
        }

        #region Overrides of Screen

        /// <inheritdoc />
        protected override void OnInitialActivate()
        {
            Update();
            Layer.RenderPropertiesUpdated += LayerOnRenderPropertiesUpdated;
            _profileEditorService.ProfileElementSelected += OnProfileElementSelected;
            _profileEditorService.SelectedProfileElementUpdated += OnSelectedProfileElementUpdated;
            _profileEditorService.ProfilePreviewUpdated += ProfileEditorServiceOnProfilePreviewUpdated;
            _panZoomViewModel.PropertyChanged += PanZoomViewModelOnPropertyChanged;
            base.OnInitialActivate();
        }

        /// <inheritdoc />
        protected override void OnClose()
        {
            Layer.RenderPropertiesUpdated -= LayerOnRenderPropertiesUpdated;
            _profileEditorService.ProfileElementSelected -= OnProfileElementSelected;
            _profileEditorService.SelectedProfileElementUpdated -= OnSelectedProfileElementUpdated;
            _profileEditorService.ProfilePreviewUpdated -= ProfileEditorServiceOnProfilePreviewUpdated;
            _panZoomViewModel.PropertyChanged -= PanZoomViewModelOnPropertyChanged;
            base.OnClose();
        }

        #endregion

        #region Event handlers

        private void LayerOnRenderPropertiesUpdated(object sender, EventArgs e)
        {
            Update();
        }

        private void OnProfileElementSelected(object sender, EventArgs e)
        {
            IsSelected = _profileEditorService.SelectedProfileElement == Layer;
        }

        private void OnSelectedProfileElementUpdated(object sender, EventArgs e)
        {
            Update();
        }

        private void ProfileEditorServiceOnProfilePreviewUpdated(object sender, EventArgs e)
        {
            if (!Layer.Transform.PropertiesInitialized || !Layer.General.PropertiesInitialized)
                return;

            CreateShapeGeometry();
            CreateViewportRectangle();
        }

        #endregion
    }
}