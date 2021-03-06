using OSPSuite.Core.Domain;

namespace PKSim.Core.Model
{
   public class TransporterContainerTemplate : WithSynonyms
   {
      /// <summary>
      ///    Gene associated with the template
      /// </summary>
      public string Gene { get; set; }

      /// <summary>
      ///    Species for which the template is defined
      /// </summary>
      public string Species { get; set; }

      /// <summary>
      ///    Organ where the transporter is defined
      /// </summary>
      public string OrganName { get; set; }

      /// <summary>
      ///    Compartment of the organ named OrganName where the transporter is defined
      /// </summary>
      public string CompartmentName { get; set; }

      /// <summary>
      ///    Transporter type => Direction of transport
      /// </summary>
      public TransportType TransportType { get; set; }

      public override string Name => Gene;
   }
}