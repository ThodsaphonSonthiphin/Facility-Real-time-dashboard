import { baseApi } from '../../shared/api/baseApi';
import { fileNameOf, type ExportSet } from './exportRules';
import { saveFile } from './saveFile';

/** ExportRequest: Thai shift dates, both included; buildingId left out for every building. */
export interface ExportRequest {
  from: string;
  to: string;
  buildingId?: number;
  sets: ExportSet[];
}

export interface ExportResult {
  fileName: string;
}

/** A downloaded file on its way from the answer to saveFile; it never reaches the store. */
interface DownloadedFile {
  blob: Blob;
  fileName: string;
}

const exportApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    /**
     * facility-0053, plan 12 decision 5. POST because every export is logged (facility-0051). The answer is the .xlsx,
     * handed to the browser here: a Blob is not serializable, so only its name enters the cache. A refusal is JSON
     * { message }; an empty body (the 401 before a refresh) reads as null, as fetchBaseQuery's own handler does, so
     * baseQueryWithReauth still sees the 401, refreshes and asks once more.
     */
    exportExcel: build.mutation<ExportResult, ExportRequest>({
      async queryFn(request, _api, _extraOptions, baseQuery) {
        const answer = await baseQuery({
          url: '/admin/export',
          method: 'POST',
          body: request,
          responseHandler: async (response) => {
            if (!response.ok) {
              const text = await response.text();
              return text ? JSON.parse(text) : null;
            }
            return { blob: await response.blob(), fileName: fileNameOf(response.headers.get('Content-Disposition'), request) };
          },
        });
        if (answer.error) {
          return { error: answer.error };
        }
        const file = answer.data as DownloadedFile;
        saveFile(file.blob, file.fileName);
        return { data: { fileName: file.fileName } };
      },
    }),
  }),
});

export const { useExportExcelMutation } = exportApi;
