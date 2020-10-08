﻿using System;
using System.Diagnostics;
using DevExpress.Utils;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraEditors.Repository;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Base;
using DevExpress.XtraGrid.Views.Grid.ViewInfo;
using OSPSuite.Assets;
using OSPSuite.Core.Domain;
using OSPSuite.Core.Domain.UnitSystem;
using OSPSuite.DataBinding.DevExpress;
using OSPSuite.DataBinding.DevExpress.XtraGrid;
using OSPSuite.Presentation.DTO;
using OSPSuite.Presentation.Views;
using OSPSuite.UI.Extensions;
using OSPSuite.UI.RepositoryItems;
using OSPSuite.UI.Services;
using OSPSuite.UI.Views;
using OSPSuite.Utility.Extensions;
using OSPSuite.Utility.Format;
using PKSim.Assets;
using PKSim.Core.Model;
using PKSim.Presentation.DTO.Individuals;
using PKSim.Presentation.Presenters.Individuals;
using PKSim.Presentation.Views.Individuals;
using PKSim.UI.Views.Core;

namespace PKSim.UI.Views.Individuals
{
   public partial class IndividualProteinExpressionsViewNew : BaseUserControlWithValueInGrid, IIndividualProteinExpressionsViewNew
   {
      private readonly IToolTipCreator _toolTipCreator;
      private readonly IImageListRetriever _imageListRetriever;
      private IIndividualProteinExpressionsPresenterNew _presenter;
      private readonly GridViewBinder<ExpressionContainerParameterDTO> _gridViewBinder;
      private IGridViewColumn _colGrouping;
      private IGridViewColumn  _colParameterValue;
      private IGridViewColumn _colParameterName;
      private ToolTipController _toolTipController;
      private readonly UxRepositoryItemButtonImage _isFixedParameterEditRepository;
      private readonly RepositoryItemTextEdit _standardParameterEditRepository = new RepositoryItemTextEdit();

      public IndividualProteinExpressionsViewNew(IToolTipCreator toolTipCreator,  IImageListRetriever imageListRetriever)
      {
         _toolTipCreator = toolTipCreator;
         _toolTipController = new ToolTipController();

         _imageListRetriever = imageListRetriever;
         InitializeComponent();
         gridView.ShouldUseColorForDisabledCell = false;
         gridView.OptionsView.AllowCellMerge =true;
         gridView.GroupFormat = "[#image]{1}";
         //gridView.AllowsFiltering = false;
         _gridViewBinder = new GridViewBinder<ExpressionContainerParameterDTO>(gridView);
         gridView.CustomColumnSort += customColumnSort;
         InitializeWithGrid(gridView);

         _isFixedParameterEditRepository = new UxRepositoryItemButtonImage(ApplicationIcons.Reset, PKSimConstants.UI.ResetParameterToolTip) { TextEditStyle = TextEditStyles.Standard };
         _toolTipController.GetActiveObjectInfo += onToolTipControllerGetActiveObjectInfo;
         _toolTipController.Initialize(_imageListRetriever);
         _standardParameterEditRepository.ConfigureWith(typeof(double));
         _standardParameterEditRepository.Appearance.TextOptions.HAlignment = HorzAlignment.Far;
         _isFixedParameterEditRepository.Buttons[0].IsLeft = true;
         gridView.GridControl.ToolTipController = _toolTipController;


      }

      //https://github.com/DevExpress-Examples/custom-gridcontrol-how-to-hide-particular-grouprow-headers-footers-t264208/blob/16.1.4%2B/CS/CustomGridControl/MyDataController.cs

      public void AttachPresenter(IIndividualProteinExpressionsPresenterNew presenter)
      {
         _presenter = presenter;
      }

      private void customColumnSort(object sender, CustomColumnSortEventArgs e)
      {
         if (e.Column != _colGrouping.XtraColumn) return;
         var container1 = e.RowObject1 as ExpressionContainerParameterDTO;
         var container2 = e.RowObject2 as ExpressionContainerParameterDTO;
         if (container1 == null || container2 == null) return;
         e.Handled = true;

         e.Result = container1.Sequence.CompareTo(container2.Sequence);
         if (e.Result != 0)
            return;

         if (container1.ContainerName != container2.ContainerName)
            return;

         //Same container and compartment, return
         if (container1.CompartmentName == container2.CompartmentName)
            return;

         //One of the two has an empty compartment. We pull this one ahead
         if (!string.IsNullOrEmpty(container1.CompartmentName) && !string.IsNullOrEmpty(container2.CompartmentName))
            return;

         // -1 will move the container1 above container 2
         e.Result = string.IsNullOrEmpty(container1.CompartmentName) ? -1 : 1;

//         Debug.Print($"{container1} && {container2}  = {e.Result}");
      }

      public override void InitializeBinding()
      {
         _colGrouping = _gridViewBinder.AutoBind(item => item.GroupingPathDTO)
            .WithRepository(dto => configureContainerRepository(dto.GroupingPathDTO))
            .WithCaption(PKSimConstants.UI.EmptyColumn)
            .AsReadOnly();
         _colGrouping.XtraColumn.GroupIndex = 0;
         _colGrouping.XtraColumn.SortMode = ColumnSortMode.Custom;

         _gridViewBinder.AutoBind(item => item.ContainerPathDTO)
            .WithRepository(dto => configureContainerRepository(dto.ContainerPathDTO))
            .WithCaption(PKSimConstants.UI.EmptyColumn)
            .AsReadOnly();

         _gridViewBinder.AutoBind(item => item.CompartmentPathDTO)
            .WithRepository(dto => configureContainerRepository(dto.CompartmentPathDTO))
            .WithCaption(PKSimConstants.UI.EmptyColumn)
            .AsReadOnly();

         _colParameterName = _gridViewBinder.AutoBind(item => item.ParameterName)
            .WithCaption(PKSimConstants.UI.Parameter)
            .AsReadOnly();
         // .WithOnValueUpdating((protein, args) => _presenter.SetRelativeExpression(protein, args.NewValue));

         //TODO MOVE TO PRESENTER
         _colParameterValue = _gridViewBinder.AutoBind(item => item.Value)
            .WithFormat(parameterFormatter)
            .WithRepository(repoForParameter)
            .WithShowButton(ShowButtonModeEnum.ShowAlways)
            .WithOnValueUpdating((o, e) => OnEvent(() => _presenter.SetParameterValue(o.Parameter, e.NewValue)))
            .WithCaption(PKSimConstants.UI.Value);

         _colParameterName.XtraColumn.OptionsColumn.AllowMerge = DefaultBoolean.False;
         _colParameterValue.XtraColumn.OptionsColumn.AllowMerge = DefaultBoolean.False;



         // var col = _gridViewBinder.AutoBind(item => item.RelativeExpressionNorm)
         //    .WithCaption(PKSimConstants.UI.RelativeExpressionNorm)
         //    .WithRepository(x => _progressBarRepository)
         //    .AsReadOnly();
         //
         // //necessary to align center since double value are aligned right by default
         // col.XtraColumn.AppearanceCell.TextOptions.HAlignment = HorzAlignment.Center;
         // col.XtraColumn.DisplayFormat.FormatType = FormatType.None;

      }

      private IFormatter<double> parameterFormatter(ExpressionContainerParameterDTO expressionContainerParameterDTO)
      {
         return expressionContainerParameterDTO.Parameter.ParameterFormatter();
      }

      private void onToolTipControllerGetActiveObjectInfo(object sender, ToolTipControllerGetActiveObjectInfoEventArgs e)
      {
         var parameterDTO = _gridViewBinder.ElementAt(e)?.Parameter;
         if (parameterDTO == null) return;

         //check if subclass want to display a tool tip as well
         var superToolTip = GetToolTipFor(parameterDTO, gridView.HitInfoAt(e.ControlMousePosition));
         if (superToolTip == null)
            return;

         e.Info = _toolTipCreator.ToolTipControlInfoFor(parameterDTO, superToolTip);
      }

      protected virtual SuperToolTip GetToolTipFor(IParameterDTO parameterDTO, GridHitInfo hi)
      {
         //don't show tooltips for value as it might contain error info that would be hidden
         if (hi.Column == _colParameterValue.XtraColumn)
            return null;

         return _toolTipCreator.ToolTipFor(parameterDTO);
      }


      private RepositoryItem repoForParameter(ExpressionContainerParameterDTO expressionContainerDTO)
      {

         if (IsSetByUser(expressionContainerDTO.Parameter))
            return _isFixedParameterEditRepository;

         return _standardParameterEditRepository;
      }

      //TODO Move to presenter
      public bool IsSetByUser(IParameterDTO parameterDTO)
      {
         if (parameterDTO.Parameter == null)
            return false;

         return parameterDTO.Parameter.ValueDiffersFromDefault();
      }

      private void resetParameter()
      {
       //TODO  _presenter.ResetParameter(_gridViewBinder.FocusedElement);
         gridView.CloseEditor();
      }

      private void setParameterUnit(IParameterDTO parameterDTO, Unit newUnit)
      {
         OnEvent(() =>
         {
            gridView.CloseEditor();
            _presenter.SetParameterUnit(parameterDTO, newUnit);
         });
      }


      private RepositoryItem configureContainerRepository(PathElement pathElement)
      {
         var containerRepository = new UxRepositoryItemImageComboBox(gridView, _imageListRetriever);
         return containerRepository.AddItem(pathElement, pathElement.IconName);
      }

      public void Clear()
      {
      }

      public void AddMoleculePropertiesView(IView view)
      {
         AddViewTo(layoutItemMoleculeProperties, view);
      }

      public void BindTo(IndividualProteinDTO individualProteinDTO)
      {
         _gridViewBinder.BindToSource(individualProteinDTO.AllExpressionContainerParameters.ToBindingList());
         gridView.BestFitColumns();

      }

      public override void InitializeResources()
      {
         base.InitializeResources();
         layoutItemMoleculeProperties.TextVisible = false;
         layoutGroupMoleculeProperties.Text = PKSimConstants.UI.Properties;
         layoutGroupMoleculeLocalization.Text = PKSimConstants.UI.Localization;
      }

      // public override bool HasError => _screenBinder.HasError || _gridViewBinder.HasError;

      public override bool HasError =>  _gridViewBinder.HasError;
   }
}
