using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace TinyBasicBlazor.Shared
{
    public class ProgramsService
    {
        private HttpClient httpClient;
        private object semaphore = new object();

        private Program GetProgram(string programId)
        {
            if (programId == null)
                throw new ArgumentNullException(nameof(programId));

            var program = Programs.SingleOrDefault(x => x.Id.ToLower() == programId.ToLower());
            return program;
        }

        private string ComposeProgramFilePath(string fileName)
        {
            return $"programs/{fileName}";
        }

        public ProgramsService(HttpClient httpClient)
        {
            this.httpClient = httpClient;
        }

        public async Task InitializeProgramsAsync()
        {
            Programs = await this.httpClient.GetFromJsonAsync<Program[]>(ComposeProgramFilePath("programs.json"));
        }

        public Program[] Programs { get; private set; }

        public string GetProgramFilePath(string programId)
        {
            if (programId == null)
                throw new ArgumentNullException(nameof(programId));

            var program = GetProgram(programId);
            if (program == null)
            {
                return null;
            }

            return ComposeProgramFilePath(program.FileName);
        }

        public async Task<string> Load(string programId)
        {
            if (programId == null)
                throw new ArgumentNullException(nameof(programId));

            var programFilePath = GetProgramFilePath(programId);
            if (programFilePath == null)
            {
                return null;
            }

            var programContent = await this.httpClient.GetStringAsync(programFilePath);
            return programContent;
        }
    }
}
