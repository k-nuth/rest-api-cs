using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Routing;

namespace bitprim.insight
{
    internal class RouteConvention : IApplicationModelConvention
    {
        private readonly AttributeRouteModel centralPrefix_;
 
        public RouteConvention(IRouteTemplateProvider routeTemplateProvider)
        {
            centralPrefix_ = new AttributeRouteModel(routeTemplateProvider);
        }

        public void Apply(ApplicationModel application)
        {
            foreach (ControllerModel controller in application.Controllers)
            {
                ProcessSelectors(controller.Selectors);

                foreach (ActionModel action in controller.Actions)
                {
                    ProcessSelectors(action.Selectors);
                }
            }
        }

        private void ProcessSelectors(IList<SelectorModel> selectors)
        {
            List<SelectorModel> matchedSelectors = selectors.Where(x => x.AttributeRouteModel != null).ToList();
            if (matchedSelectors.Any())
            {
                foreach (SelectorModel selectorModel in matchedSelectors)
                {
                    selectorModel.AttributeRouteModel.Template = "/" + 
                                                                 AttributeRouteModel.CombineTemplates(centralPrefix_.Template,
                                                                     selectorModel.AttributeRouteModel.Template);
                }
            }
 
            List<SelectorModel> unmatchedSelectors = selectors.Where(x => x.AttributeRouteModel == null).ToList();
            if (unmatchedSelectors.Any())
            {
                foreach (SelectorModel selectorModel in unmatchedSelectors)
                {
                    selectorModel.AttributeRouteModel = centralPrefix_;
                }
            }
        }
    }
}