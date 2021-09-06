import { PageSplitOption } from './page-split-option';
import { READER_MODE } from './reader-mode';
import { ReadingDirection } from './reading-direction';
import { ScalingOption } from './scaling-option';

export interface Preferences {
    // Manga Reader
    readingDirection: ReadingDirection;
    scalingOption: ScalingOption;
    pageSplitOption: PageSplitOption;
    readerMode: READER_MODE;
    autoCloseMenu: boolean;
    
    // Book Reader
    bookReaderDarkMode: boolean;
    bookReaderMargin: number;
    bookReaderLineSpacing: number;
    bookReaderFontSize: number;
    bookReaderFontFamily: string;
    bookReaderTapToPaginate: boolean;
    bookReaderReadingDirection: ReadingDirection;

    // Global
    siteDarkMode: boolean;
}

export const readingDirections = [{text: '从左到右', value: ReadingDirection.LeftToRight}, {text: '从右到左', value: ReadingDirection.RightToLeft}];
export const scalingOptions = [{text: '自动', value: ScalingOption.Automatic}, {text: '适应高度', value: ScalingOption.FitToHeight}, {text: '适应宽度', value: ScalingOption.FitToWidth}, {text: '原始大小', value: ScalingOption.Original}];
export const pageSplitOptions = [{text: '从右到左', value: PageSplitOption.SplitRightToLeft}, {text: '从左到右', value: PageSplitOption.SplitLeftToRight}, {text: '不分页', value: PageSplitOption.NoSplit}];
export const readingModes = [{text: '从左到右', value: READER_MODE.MANGA_LR}, {text: '从上到下', value: READER_MODE.MANGA_UD}, {text: '滚动', value: READER_MODE.WEBTOON}];
