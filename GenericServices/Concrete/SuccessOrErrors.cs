﻿using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity.Validation;
using System.Linq;

namespace GenericServices.Concrete
{

    public class SuccessOrErrors : ISuccessOrErrors
    {

        private List<ValidationResult> _errors;
        /// <summary>
        /// Holds the list of errors. Empty list means no errors. Null means validation has not been done
        /// </summary>
        public IReadOnlyList<ValidationResult> Errors { get { return _errors; }}

        /// <summary>
        /// Returns true if not errors or not validated yet, else false. 
        /// </summary>
        public bool IsValid { get { return ( Errors != null && Errors.Count == 0); }}

        public string SuccessMessage { get; private set; }

        //---------------------------------------------------
        //public methods

        /// <summary>
        /// This converts the Entity framework errors into Validation errors
        /// </summary>
        public ISuccessOrErrors SetErrors(IEnumerable<DbEntityValidationResult> errors)
        {
            _errors = new List<ValidationResult>();

            foreach (var errorsPerThisClass in errors)
                _errors.AddRange(errorsPerThisClass.ValidationErrors.Select(y => new ValidationResult(y.ErrorMessage, new[] { y.PropertyName })));

            SuccessMessage = string.Empty;
            return this;
        }

        /// <summary>
        /// This sets the error list to a series of non property specific error messages
        /// </summary>
        /// <param name="errors"></param>
        public ISuccessOrErrors SetErrors(IEnumerable<string> errors)
        {
            _errors = errors.Where(x => !string.IsNullOrEmpty(x)).Select(x => new ValidationResult(x)).ToList();
            SuccessMessage = string.Empty;
            return this;
        }

        /// <summary>
        /// Allows a single error to be added
        /// </summary>
        /// <param name="errorformat"></param>
        /// <returns></returns>
        public ISuccessOrErrors AddSingleError(string errorformat, params object[] args)
        {
            if (_errors == null)
                _errors = new List<ValidationResult>();
            _errors.Add(new ValidationResult(string.Format(errorformat, args)));
            SuccessMessage = string.Empty;
            return this;
        }

        /// <summary>
        /// This sets a success message and sets the IsValid flag to true
        /// </summary>
        /// <param name="successformat"></param>
        public ISuccessOrErrors SetSuccessMessage(string successformat, params object [] args)
        {
            _errors = new List<ValidationResult>();         //empty list means its been validated and its Valid
            SuccessMessage = string.Format(successformat, args);
            return this;
        }

        /// <summary>
        /// This is a quick way to create an ISuccessOrErrors with a success message
        /// </summary>
        /// <param name="formattedSuccessMessage"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public static ISuccessOrErrors Success(string formattedSuccessMessage, params object[] args)
        {
            return new SuccessOrErrors().SetSuccessMessage(string.Format(formattedSuccessMessage, args));
        }

        /// <summary>
        /// Useful one line error statement where brevity is needed
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            if (IsValid)
                return SuccessMessage ?? "The task completed successfully";

            return string.Format("Failed with {0} errors", _errors.Count);
        }

    }
}