﻿using NetFusion.Domain.Entity;
using System;
using System.Collections.Generic;

namespace NetFusion.Messaging
{
    /// <summary>
    /// Default implementation representing message that can have one and only
    /// one consumer and handler.  The handling consumer can associate a 
    /// result after processing the message.
    /// </summary>
    public abstract class Command : ICommand
    {
        private IEntityAttributes _attributes;
        public object Result { get; set; }

        public Command()
        {
            _attributes = new EntityAttributes();
        }

        public IEntityAttributes Attributes => _attributes;

        public IDictionary<string, object> AttributeValues
        {
            get { return _attributes.GetValues(); }
            set { _attributes.SetValues(value); }
        }

        public Type ResultType => null;

        void ICommand.SetResult(object result)
        {
            this.Result = result;
        }






        private IDictionary<string, object> _attributesOld;
        public IDictionary<string, object> AttributesOld
        {
            get
            {
                return _attributesOld ?? (_attributesOld = new Dictionary<string, object>());
            }
        }
    }

    /// <summary>
    /// Default implementation representing a message that can have one and only one 
    /// consumer and handler.  The handling consumer can associate a result 
    /// after processing the message.
    /// </summary>
    /// <typeparam name="TResult">The response set by the consumer that processed
    /// the message.</typeparam>
    public abstract class Command<TResult> : ICommand<TResult>
    {
        private IEntityAttributes _attributes;
        public TResult Result { get; set; }

        public Command()
        {
            _attributes = new EntityAttributes();
        }

        public IEntityAttributes Attributes => _attributes;

        public IDictionary<string, object> AttributeValues
        {
            get { return _attributes.GetValues(); }
            set { _attributes.SetValues(value); }
        }

        Type ICommand.ResultType => typeof(TResult);

        void ICommand.SetResult(object result)
        {
            this.Result = (TResult)result;
        }





        private IDictionary<string, object> _attributesOld;
        public IDictionary<string, object> AttributesOld
        {
            get
            {
                return _attributesOld ?? (_attributesOld = new Dictionary<string, object>());
            }
        }
    }
}
