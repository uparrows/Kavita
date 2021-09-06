import { MangaFormat } from "./manga-format";

export interface FilterItem {
    title: string;
    value: any;
    selected: boolean;
}

export interface SeriesFilter {
    mangaFormat: MangaFormat | null;
}

export const mangaFormatFilters = [
    {
      title: '格式: 全部',
      value: null,
      selected: false
    },
    {
      title: '格式: 图片',
      value: MangaFormat.IMAGE,
      selected: false
    },
    {
      title: '格式: EPUB',
      value: MangaFormat.EPUB,
      selected: false
    },
    {
      title: '格式: PDF',
      value: MangaFormat.PDF,
      selected: false
    },
    {
      title: '格式: 压缩包',
      value: MangaFormat.ARCHIVE,
      selected: false
    }
];