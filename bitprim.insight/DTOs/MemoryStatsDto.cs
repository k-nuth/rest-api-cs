namespace bitprim.insight.DTOs
{
    /// <summary>
    /// Json Dto for mem stats
    /// </summary>
    public class MemoryStatsDto
    {
        /// <summary>
        /// 
        /// </summary>
        public long mem_gc_total_memory { get;  }
        
        /// <summary>
        /// 
        /// </summary>
        public long mem_gc_collection_count_0 { get;  }
        
        /// <summary>
        /// 
        /// </summary>
        public long mem_gc_collection_count_1 { get;  }
        
        /// <summary>
        /// 
        /// </summary>
        public long mem_gc_collection_count_2 { get;  }

        /// <summary>
        /// 
        /// </summary>
        public long mem_proc_virtual_memory_size { get;  }
        
        /// <summary>
        /// 
        /// </summary>
        public long mem_proc_working_set { get;  }
        
        /// <summary>
        /// 
        /// </summary>
        public long mem_proc_nonpaged_system_memory_size { get;  }
        
        /// <summary>
        /// 
        /// </summary>
        public long mem_proc_paged_memory_size { get;  }
        /// <summary>
        /// 
        /// </summary>
        public long mem_proc_paged_system_memory_size { get;  }
        
        /// <summary>
        /// 
        /// </summary>
        public long mem_proc_peak_paged_memory_size { get;  }
        
        /// <summary>
        /// 
        /// </summary>
        public long mem_proc_peak_working_set { get;  }
        
        /// <summary>
        /// 
        /// </summary>
        public long mem_proc_private_memory_size { get;  }
        
        /// <summary>
        /// 
        /// </summary>
        public long mem_proc_peak_virtual_memory_size { get;  }
        
        /// <summary>
        /// 
        /// </summary>
        public int mem_proc_threads_count { get;  }

        /// <summary>
        /// 
        /// </summary>
        public int pool_max_worker_threads { get; }


        /// <summary>
        /// 
        /// </summary>
        public int pool_max_completition_port_threads { get; }


        /// <summary>
        /// 
        /// </summary>
        public int pool_available_worker_threads { get; }


        /// <summary>
        /// 
        /// </summary>
        public int pool_available_completition_port_threads { get; }

        /// <summary>
        /// 
        /// </summary>
        public MemoryStatsDto()
        {
            mem_gc_total_memory = System.GC.GetTotalMemory(false);
            mem_gc_collection_count_0 = System.GC.CollectionCount(0);
            mem_gc_collection_count_1 = System.GC.CollectionCount(1);
            mem_gc_collection_count_2 = System.GC.CollectionCount(2);

            using (var proc = System.Diagnostics.Process.GetCurrentProcess())
            {
                mem_proc_virtual_memory_size = proc.VirtualMemorySize64;
                mem_proc_working_set = proc.WorkingSet64;
                mem_proc_nonpaged_system_memory_size = proc.NonpagedSystemMemorySize64;
                mem_proc_paged_memory_size = proc.PagedMemorySize64;
                mem_proc_paged_system_memory_size = proc.PagedSystemMemorySize64;
                mem_proc_peak_paged_memory_size = proc.PeakPagedMemorySize64;
                mem_proc_peak_working_set = proc.PeakWorkingSet64;
                mem_proc_private_memory_size = proc.PrivateMemorySize64;
                mem_proc_peak_virtual_memory_size = proc.PeakVirtualMemorySize64;
                mem_proc_threads_count = proc.Threads.Count;
            }

            System.Threading.ThreadPool.GetMaxThreads(out var temp_pool_max_worker_threads, out var temp_pool_max_completition_port_threads);

            pool_max_worker_threads = temp_pool_max_worker_threads;
            pool_max_completition_port_threads = temp_pool_max_completition_port_threads;

            System.Threading.ThreadPool.GetAvailableThreads(out var temp_pool_available_worker_threads, out var temp_pool_available_completition_port_threads);
            pool_available_worker_threads = temp_pool_available_worker_threads;
            pool_available_completition_port_threads = temp_pool_available_completition_port_threads;
        }
    }
}