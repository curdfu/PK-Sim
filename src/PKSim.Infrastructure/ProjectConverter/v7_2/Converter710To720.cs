﻿using System.Linq;
using System.Xml.Linq;
using OSPSuite.Core.Domain;
using OSPSuite.Core.Domain.Services;
using OSPSuite.Utility.Visitor;
using PKSim.Core;
using PKSim.Core.Model;
using PKSim.Core.Repositories;
using PKSim.Core.Services;

namespace PKSim.Infrastructure.ProjectConverter.v7_2
{
   public class Converter710To720 : IObjectConverter,
      IVisitor<Individual>,
      IVisitor<Population>,
      IVisitor<IndividualSimulation>,
      IVisitor<PopulationSimulation>

   {
      private readonly IDefaultIndividualRetriever _defaultIndividualRetriever;
      private readonly ICloner _cloner;
      private readonly ICalculationMethodRepository _calculationMethodRepository;
      private readonly IEntityPathResolver _entityPathResolver;
      private readonly IOrganTypeRepository _organTypeRepository;

      public Converter710To720(IDefaultIndividualRetriever defaultIndividualRetriever, ICloner cloner, ICalculationMethodRepository calculationMethodRepository, IEntityPathResolver entityPathResolver, IOrganTypeRepository organTypeRepository)
      {
         _defaultIndividualRetriever = defaultIndividualRetriever;
         _cloner = cloner;
         _calculationMethodRepository = calculationMethodRepository;
         _entityPathResolver = entityPathResolver;
         _organTypeRepository = organTypeRepository;
      }

      public bool IsSatisfiedBy(int version)
      {
         return version == ProjectVersions.V7_1_0;
      }

      public int Convert(object objectToConvert, int originalVersion)
      {
         this.Visit(objectToConvert);
         return ProjectVersions.V7_2_0;
      }

      public int ConvertXml(XElement element, int originalVersion)
      {
         convertOrganTypes(element);
         return ProjectVersions.V7_2_0;
      }

      private void convertOrganTypes(XElement element)
      {
         var allOrganTypesAttributes = from child in element.DescendantsAndSelf()
            where child.HasAttributes
            let attr = child.Attribute(ConverterConstants.Serialization.Attribute.ORGAN_TYPE)
            where attr != null
            select attr;

         foreach (var organTypesAttribute in allOrganTypesAttributes)
         {
            organTypesAttribute.SetValue(convertOrganType(organTypesAttribute.Value));
         }
      }

      private OrganType convertOrganType(string oldOrganType)
      {
         return _organTypeRepository.OrganTypeFor(organName: oldOrganType);
      }

      public void Visit(Individual individual)
      {
         convertIndividual(individual);
      }

      public void Visit(Population population)
      {
         convertIndividual(population.FirstIndividual);
         addBSAParameterValues(population);
      }

      private void convertIndividual(Individual individual)
      {
         if (individual == null)
            return;

         if (!individual.IsHuman)
            return;

         if (individual.Organism.Parameter(CoreConstants.Parameter.BSA) != null)
            return;

         var defaultHuman = _defaultIndividualRetriever.DefaultHuman();
         var bsa = defaultHuman.Organism.Parameter(CoreConstants.Parameter.BSA);

         individual.Organism.Add(_cloner.Clone(bsa));

         addBSACalculationMethod(individual);
      }

      private void addBSAParameterValues(Population population)
      {
         if (!population.IsHuman)
            return;

         var defaultHuman = _cloner.Clone(_defaultIndividualRetriever.DefaultHuman());

         var heightParameter = defaultHuman.Organism.Parameter(CoreConstants.Parameter.HEIGHT);
         var weightParameter = defaultHuman.Organism.Parameter(CoreConstants.Parameter.WEIGHT);
         var bsaParameter = defaultHuman.Organism.Parameter(CoreConstants.Parameter.BSA);

         var allWeights = population.AllOrganismValuesFor(weightParameter.Name, _entityPathResolver);
         var allHeights = population.AllOrganismValuesFor(heightParameter.Name, _entityPathResolver);

         if (allHeights.Count != allWeights.Count)
            return;

         var parameterValues = new ParameterValues(_entityPathResolver.PathFor(bsaParameter));
         for (int i = 0; i < allWeights.Count; i++)
         {
            weightParameter.Value = allWeights[i];
            heightParameter.Value = allHeights[i];
            parameterValues.Add(bsaParameter.Value);
         }

         population.IndividualPropertiesCache.Add(parameterValues);
      }

      private void addBSACalculationMethod(Individual individual)
      {
         if (!individual.Species.IsHuman)
            return;

         individual.OriginData.AddCalculationMethod(_calculationMethodRepository.FindByName(ConverterConstants.CalculationMethod.BSA_DuBois));
      }

      public void Visit(IndividualSimulation individualSimulation)
      {
         Visit(individualSimulation.Individual);
      }

      public void Visit(PopulationSimulation populationSimulation)
      {
         Visit(populationSimulation.Population);
      }
   }
}