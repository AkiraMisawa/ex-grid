npm install @ex-grid/react react react-dom
npm install @ex-grid/sheet-react          # the sheet; @ex-grid/sheet-engine alone computes, on a server too
npm install @ex-grid/pivot-react          # the pivot table; @ex-grid/pivot-engine alone aggregates
npm install @ex-grid/chart-react          # charts
npm install @ex-grid/board-react          # a Board of grids, pivots and charts
npm install @ex-grid/sheet-xlsx           # a Workbook opened from and saved to .xlsx
npm install temporal-polyfill             # where the browser has no Temporal, for a date column's filter

# In a Mantine application, each product's Wrapper as well
npm install @ex-grid/mantine @ex-grid/sheet-mantine @ex-grid/pivot-mantine @ex-grid/chart-mantine

# In a MUI application, each product's Wrapper as well
npm install @ex-grid/mui @ex-grid/sheet-mui @ex-grid/pivot-mui @ex-grid/chart-mui
