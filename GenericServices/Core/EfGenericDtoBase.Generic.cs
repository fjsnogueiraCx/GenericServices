﻿#region licence
// The MIT License (MIT)
// 
// Filename: EfGenericDtoBase.Generic.cs
// Date Created: 2014/07/21
// 
// Copyright (c) 2014 Jon Smith (www.selectiveanalytics.com & www.thereformedprogrammer.net)
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
#endregion

using System.Data;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DelegateDecompiler;
using GenericLibsBase;
using GenericLibsBase.Core;
using GenericServices.Core.Internal;

[assembly: InternalsVisibleTo("Tests")]

namespace GenericServices.Core
{
    
    public abstract class EfGenericDtoBase<TEntity, TDto> : EfGenericDtoBase
        where TEntity : class
        where TDto : EfGenericDtoBase<TEntity, TDto>
    {
        /// <summary>
        /// Optional method that will setup any mapping etc. that are cached. This will will improve speed later.
        /// The GenericDto will still work without this method being called, but the first use that needs the map will be slower. 
        /// </summary>
        public void CacheSetup()
        {
            CreateDatatoDtoMapping();
            CreateDtoToDataMapping();
        }

        /// <summary>
        /// This provides the name of the name of the data item to display in success or error messages.
        /// Override if you want a more user friendly name
        /// </summary>
        internal protected virtual string DataItemName { get { return typeof (TEntity).Name; }}
        
        /// <summary>
        /// This method is called to get the data table. Can be overridden if include statements are needed.
        /// </summary>
        /// <param name="context"></param>
        /// <returns>returns an IQueryable of the table TEntity as Untracked</returns>
        protected virtual IQueryable<TEntity> GetDataUntracked(IGenericServicesDbContext context)
        {
            return context.Set<TEntity>().AsNoTracking();
        }

        /// <summary>
        /// This provides the IQueryable command to get a list of TEntity, but projected to TDto.
        /// Can be overridden if standard AutoMapping isn't good enough, or return null if not supported
        /// </summary>
        /// <returns></returns>
        internal protected virtual IQueryable<TDto> ListQueryUntracked(IGenericServicesDbContext context)
        {
            CreateDatatoDtoMapping();
            var query = GetDataUntracked(context).Project().To<TDto>();

            //We check if we need to decompile the LINQ expression so that any computed properties in the class are filled in properly
            return ApplyDecompileIfNeeded(query);
        }

        /// <summary>
        /// This copies back the keys from a newly created entity into the dto as long as there are matching properties in the Dto
        /// </summary>
        /// <param name="context"></param>
        /// <param name="newEntity"></param>
        internal protected void AfterCreateCopyBackKeysToDtoIfPresent(IGenericServicesDbContext context, TEntity newEntity)
        {
            var dtoKeyProperies = typeof (TDto).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var entityKeys in context.GetKeyProperties<TEntity>())
            {
                var dtoMatchingProperty =
                    dtoKeyProperies.SingleOrDefault(
                        x => x.Name == entityKeys.Name && x.PropertyType == entityKeys.PropertyType);
                if (dtoMatchingProperty == null) continue;

                dtoMatchingProperty.SetValue(this, entityKeys.GetValue(newEntity));
            }
        }


        //---------------------------------------------------------------
        //protected methods

        protected object[] GetKeyValues(IGenericServicesDbContext context)
        {
            var efkeyPropertyNames = context.GetKeyProperties<TEntity>().ToArray();

            var dtoKeyProperies = typeof(TDto).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(x => efkeyPropertyNames.Any(y => y.Name == x.Name && y.PropertyType == x.PropertyType)).ToArray();

            if (efkeyPropertyNames.Length != dtoKeyProperies.Length)
                throw new MissingPrimaryKeyException("The dto must contain the key(s) properties from the data class.");

            return dtoKeyProperies.Select(x => x.GetValue(this)).ToArray();
        }

        /// <summary>
        /// This sets up the AutoMapper mapping for a copy from the TEntity to the TDto.
        /// </summary>
        protected static void CreateDatatoDtoMapping()
        {
            Mapper.CreateMap<TEntity, TDto>();
        }

        /// <summary>
        /// This sets up the AutoMapper mapping for a copy from the TDto to the TEntity.
        /// Note that properties which have the [DoNotCopyBackToDatabase] attribute will not be copied
        /// </summary>
        protected static void CreateDtoToDataMapping()
        {
            Mapper.CreateMap<TDto, TEntity>()
                .ForAllMembers(opt => opt.Condition(IncludeIfSourceDoesNotHaveDoNotCopyBackToDatabaseAttribute));
        }

        /// <summary>
        /// This copies only the properties in that do not have the [DoNotCopyBackToDatabase] attribute
        /// </summary>
        /// <param name="context"></param>
        /// <param name="source"></param>
        /// <param name="destination"></param>
        protected ISuccessOrErrors CreateUpdateDataFromDto(IGenericServicesDbContext context, TDto source, TEntity destination)
        {
            CreateDtoToDataMapping();
            Mapper.Map(source, destination);
            return SuccessOrErrors.Success("Successful copy of data");
        }

        /// <summary>
        /// This checks if the DelegateDecompiler is needed. If so it applies it to the query
        /// </summary>
        /// <returns>original query, but with Decompile applied if needed</returns>
        protected IQueryable<TDto> ApplyDecompileIfNeeded(IQueryable<TDto> query)
        {
            var shouldDecompile = RequiresDelegateDecompiler || (GenericServicesConfig.UseDelegateDecompilerWhereNeeded &&
                                  typeof(TEntity).GetProperties()
                                      .Any(x => x.GetCustomAttribute<ComputedAttribute>() != null));
            return shouldDecompile ? query.Decompile() : query;
        }

        //----------------------------------------------------------------
        //private methods

        private static bool IncludeIfSourceDoesNotHaveDoNotCopyBackToDatabaseAttribute(ResolutionContext mapContext)
        {
            return mapContext.PropertyMap.SourceMember != null &&
                   mapContext.PropertyMap.SourceMember.GetCustomAttribute<DoNotCopyBackToDatabaseAttribute>() == null;
        }

    }
}
