using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;

namespace QDLogistics.Models.Repositiry
{
    public class GenericRepository<TEntity> : IRepository<TEntity> where TEntity : class
    {
        internal QDLogisticsEntities db;
        internal DbSet<TEntity> dbSet;

        public GenericRepository()
            : this(new QDLogisticsEntities())
        {
        }

        public GenericRepository(QDLogisticsEntities db)
        {
            this.db = db;
            this.dbSet = db.Set<TEntity>();
        }

        public void Create(TEntity entity)
        {
            dbSet.Add(entity);
        }

        public void Update(TEntity entity)
        {
            db.Entry(entity).State = EntityState.Modified;
        }

        public void Update(TEntity entity, params object[] keyValues)
        {
            if (db.Entry(entity).State == EntityState.Detached)
            {
                TEntity attachedEntity = dbSet.Find(keyValues);

                if (attachedEntity != null)
                {
                    var attachedEntry = db.Entry(attachedEntity);
                    attachedEntry.CurrentValues.SetValues(entity);
                }
                else
                {
                    db.Entry(entity).State = EntityState.Modified;
                }
            }
        }

        public void Delete(int id)
        {
            TEntity entity = dbSet.Find(id);
            Delete(entity);
        }

        public void Delete(TEntity entity)
        {
            if (db.Entry(entity).State == EntityState.Detached)
            {
                dbSet.Attach(entity);
            }
            dbSet.Remove(entity);
        }

        public TEntity Get(int id)
        {
            return dbSet.Find(id);
        }

        public TEntity Get(string id)
        {
            return dbSet.Find(id);
        }

        public IEnumerable<TEntity> GetAll(bool noTracking)
        {
            IEnumerable<TEntity> query = noTracking ? dbSet.AsNoTracking() : dbSet;
            return query;
        }

        public void SaveChanges()
        {
            try
            {
                db.SaveChanges();
            }
            catch (System.Data.Entity.Validation.DbEntityValidationException ex)
            {
                throw ex;
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (db != null)
                {
                    db.Dispose();
                    db = null;
                }
            }
        }
    }
}